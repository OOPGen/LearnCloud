using LearnCloud.AttendanceTimetable.DTOs;
using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.AttendanceTimetable.Services;

public interface IAttendanceService
{
    Task<TenantAttendanceSettingsDto> GetSettingsAsync(long tenantId, CancellationToken ct = default);
    Task<TenantAttendanceSettingsDto> UpdateSettingsAsync(long tenantId, UpdateAttendanceSettingsRequest req, long userId, CancellationToken ct = default);
    Task<RegisterResponseDto> GetRegisterAsync(long tenantId, GetRegisterRequest req, CancellationToken ct = default);
    Task<MarkRegisterResponse> MarkRegisterAsync(long tenantId, long userId, MarkRegisterRequest req, string ip, CancellationToken ct = default);
    Task<AttendanceSummaryPerClassDto> GetSummaryAsync(long tenantId, AttendanceSummaryRequest req, CancellationToken ct = default);
    Task<PrintableMonthRegisterDto> GetPrintableMonthAsync(long tenantId, PrintableMonthRegisterRequest req, CancellationToken ct = default);
    decimal CalculatePercentage(int present, int late, int excused, int sick, int absent, int total, TenantAttendanceSettings settings);
}

public class AttendanceService : IAttendanceService
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AttendanceService(LearnCloudDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TenantAttendanceSettingsDto> GetSettingsAsync(long tenantId, CancellationToken ct = default)
    {
        var settings = await _db.Set<TenantAttendanceSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (settings == null)
        {
            // Sensible defaults
            settings = new TenantAttendanceSettings { TenantId = tenantId, Mode = AttendanceMode.Daily, BackdatingWindowDays = 7, ChronicAbsenceThreshold = 85m, CountLateAsPresent = true, CountExcusedAsPresent = true };
            _db.Set<TenantAttendanceSettings>().Add(settings);
            await _db.SaveChangesAsync(ct);
        }

        return new TenantAttendanceSettingsDto(
            settings.Mode == AttendanceMode.Daily ? "daily" : "per_period",
            settings.BackdatingWindowDays,
            settings.AllowBackdatingBeyondWindow,
            settings.ChronicAbsenceThreshold,
            settings.CountLateAsPresent,
            settings.CountExcusedAsPresent,
            settings.CountSickAsPresent
        );
    }

    public async Task<TenantAttendanceSettingsDto> UpdateSettingsAsync(long tenantId, UpdateAttendanceSettingsRequest req, long userId, CancellationToken ct = default)
    {
        var settings = await _db.Set<TenantAttendanceSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (settings == null)
        {
            settings = new TenantAttendanceSettings { TenantId = tenantId };
            _db.Set<TenantAttendanceSettings>().Add(settings);
        }

        settings.Mode = req.Mode == "daily" ? AttendanceMode.Daily : AttendanceMode.PerPeriod;
        settings.BackdatingWindowDays = req.BackdatingWindowDays;
        settings.ChronicAbsenceThreshold = req.ChronicAbsenceThreshold;
        settings.CountLateAsPresent = req.CountLateAsPresent;
        settings.CountExcusedAsPresent = req.CountExcusedAsPresent;
        settings.CountSickAsPresent = req.CountSickAsPresent;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);
        return await GetSettingsAsync(tenantId, ct);
    }

    public async Task<RegisterResponseDto> GetRegisterAsync(long tenantId, GetRegisterRequest req, CancellationToken ct = default)
    {
        // Get grade/stream names
        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == req.GradeId && g.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Grade not found");
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == req.StreamId && s.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Stream not found");

        // The class list is the students whose current enrolment is in this class. An
        // enrolment's TermId is the term the student joined in, so filtering on the register's
        // term dropped everyone who joined in an earlier term. The fallback to students by
        // grade and stream is gone too: it listed students who had left the school.
        var students = await _db.Set<Student>().Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Join(_db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.GradeId == req.GradeId && e.StreamId == req.StreamId && e.AcademicYearId == req.AcademicYearId && e.IsCurrent && !e.IsDeleted),
                s => s.Id, e => e.StudentId, (s, e) => s)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        var register = await _db.Set<AttendanceRegister>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.GradeId == req.GradeId && r.StreamId == req.StreamId && r.AttendanceDate == req.AttendanceDate.Date && r.PeriodNumber == req.PeriodNumber && r.AcademicYearId == req.AcademicYearId && r.TermId == req.TermId && !r.IsDeleted, ct);

        var records = new List<AttendanceRecord>();
        if (register != null)
        {
            records = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.RegisterId == register.Id && !a.IsDeleted).ToListAsync(ct);
        }

        var settings = await _db.Set<TenantAttendanceSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        var periodName = req.PeriodNumber.HasValue ? $"Period {req.PeriodNumber}" : "Daily";

        var header = new RegisterHeaderDto(
            register?.Id ?? 0,
            req.GradeId,
            req.StreamId,
            grade.Name,
            stream.Name,
            req.AttendanceDate.Date,
            req.PeriodNumber,
            periodName,
            students.Count,
            records.Count,
            register?.IsBackdated ?? false,
            register?.Status ?? "draft"
        );

        var studentDtos = students.Select(s => new StudentForRegisterDto(s.Id, s.StudentNumber, s.FirstName, s.LastName, s.PhotoUrl ?? "", records.FirstOrDefault(r => r.StudentId == s.Id)?.Status.ToString().ToLower() ?? "unmarked")).ToList();

        var recordDtos = records.Select(r => new AttendanceRecordDto(r.Id, r.StudentId, r.Status.ToString().ToLower(), r.AbsenceReason, r.Note, r.IsBackdated, r.MarkedAt)).ToList();

        return new RegisterResponseDto(header, studentDtos, recordDtos);
    }

    public async Task<MarkRegisterResponse> MarkRegisterAsync(long tenantId, long userId, MarkRegisterRequest req, string ip, CancellationToken ct = default)
    {
        var settings = await _db.Set<TenantAttendanceSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (settings == null)
        {
            settings = new TenantAttendanceSettings { TenantId = tenantId, Mode = AttendanceMode.Daily, BackdatingWindowDays = 7, ChronicAbsenceThreshold = 85m };
            _db.Set<TenantAttendanceSettings>().Add(settings);
            await _db.SaveChangesAsync(ct);
        }

        // Guard against duplicate registers for same class, date and period
        var existingRegister = await _db.Set<AttendanceRegister>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.GradeId == req.GradeId && r.StreamId == req.StreamId && r.AttendanceDate == req.AttendanceDate.Date && r.PeriodNumber == req.PeriodNumber && r.AcademicYearId == req.AcademicYearId && r.TermId == req.TermId && !r.IsDeleted, ct);

        var isBackdated = false;
        string? backdateReason = req.BackdateReason;
        var daysDiff = (DateTime.UtcNow.Date - req.AttendanceDate.Date).TotalDays;
        if (daysDiff > settings.BackdatingWindowDays)
        {
            isBackdated = true;
            if (!settings.AllowBackdatingBeyondWindow && daysDiff > settings.BackdatingWindowDays)
            {
                throw new InvalidOperationException($"Backdating beyond {settings.BackdatingWindowDays} days not allowed. Requested {daysDiff} days ago. Flagged in audit log.");
            }
        }

        // One unit of work under the retrying execution strategy (see InTransactionAsync).
        return await _db.InTransactionAsync(async () =>
        {
            // Re-read inside the unit so a retry works from fresh, tracked state.
            var currentRegister = existingRegister is null ? null
                : await _db.Set<AttendanceRegister>().FirstOrDefaultAsync(r => r.Id == existingRegister.Id && r.TenantId == tenantId, ct);

            AttendanceRegister register;
            if (currentRegister == null)
            {
                register = new AttendanceRegister
                {
                    TenantId = tenantId,
                    AcademicYearId = req.AcademicYearId,
                    TermId = req.TermId,
                    GradeId = req.GradeId,
                    StreamId = req.StreamId,
                    AttendanceDate = req.AttendanceDate.Date,
                    PeriodNumber = req.PeriodNumber,
                    Status = "submitted",
                    IsBackdated = isBackdated,
                    SubmittedAt = DateTime.UtcNow,
                    SubmittedByUserId = userId,
                    CreatedBy = userId
                };
                _db.Set<AttendanceRegister>().Add(register);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                register = currentRegister;
                register.Status = "submitted";
                register.SubmittedAt = DateTime.UtcNow;
                register.SubmittedByUserId = userId;
                register.IsBackdated = isBackdated || register.IsBackdated;
                await _db.SaveChangesAsync(ct);

                // Delete existing records for this register to replace (upsert)
                var oldRecords = await _db.Set<AttendanceRecord>().Where(r => r.RegisterId == register.Id && r.TenantId == tenantId).ToListAsync(ct);
                _db.Set<AttendanceRecord>().RemoveRange(oldRecords);
                await _db.SaveChangesAsync(ct);
            }

            // Create new records
            foreach (var item in req.Items)
            {
                var rec = new AttendanceRecord
                {
                    TenantId = tenantId,
                    RegisterId = register.Id,
                    StudentId = item.StudentId,
                    GradeId = req.GradeId,
                    StreamId = req.StreamId,
                    AcademicYearId = req.AcademicYearId,
                    TermId = req.TermId,
                    AttendanceDate = req.AttendanceDate.Date,
                    PeriodNumber = req.PeriodNumber,
                    Status = Enum.Parse<AttendanceStatus>(item.Status, true),
                    AbsenceReason = item.AbsenceReason,
                    Note = item.Note,
                    MarkedByUserId = userId,
                    MarkedAt = DateTime.UtcNow,
                    IsBackdated = isBackdated,
                    BackdateReason = backdateReason,
                    CreatedBy = userId
                };
                _db.Set<AttendanceRecord>().Add(rec);
            }

            await _db.SaveChangesAsync(ct);

            // Audit log for backdating flagged
            if (isBackdated)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    TenantId = tenantId,
                    UserId = userId,
                    EntityType = "AttendanceRegister",
                    EntityId = register.Id,
                    Action = "backdated_attendance",
                    OldValues = $"{{\"date\":\"{req.AttendanceDate:o}\",\"daysDiff\":{daysDiff}}}",
                    NewValues = $"{{\"backdateReason\":\"{backdateReason}\",\"windowDays\":{settings.BackdatingWindowDays}}}",
                    IpAddress = ip,
                    Reason = $"Backdated {daysDiff} days beyond window {settings.BackdatingWindowDays}"
                });
                await _db.SaveChangesAsync(ct);
            }

            return new MarkRegisterResponse(register.Id, req.Items.Count, req.Items.Count, isBackdated, DateTime.UtcNow, isBackdated ? $"Saved with backdate flag ({daysDiff} days ago)" : "Saved");
        }, ct);
    }

    public decimal CalculatePercentage(int present, int late, int excused, int sick, int absent, int total, TenantAttendanceSettings settings)
    {
        if (total == 0) return 0m;
        int presentCount = present;
        if (settings.CountLateAsPresent) presentCount += late;
        if (settings.CountExcusedAsPresent) presentCount += excused;
        if (settings.CountSickAsPresent) presentCount += sick;

        // Percentage = presentCount / total * 100
        return Math.Round((decimal)presentCount / total * 100m, 2);
    }

    public async Task<AttendanceSummaryPerClassDto> GetSummaryAsync(long tenantId, AttendanceSummaryRequest req, CancellationToken ct = default)
    {
        var settings = await _db.Set<TenantAttendanceSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
                       ?? new TenantAttendanceSettings { TenantId = tenantId, ChronicAbsenceThreshold = 85m, CountLateAsPresent = true, CountExcusedAsPresent = true };

        // Get grades/streams filtered
        var query = _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && !a.IsDeleted && a.AcademicYearId == req.AcademicYearId && a.TermId == req.TermId);

        if (req.GradeId.HasValue) query = query.Where(a => a.GradeId == req.GradeId.Value);
        if (req.StreamId.HasValue) query = query.Where(a => a.StreamId == req.StreamId.Value);
        if (req.FromDate.HasValue) query = query.Where(a => a.AttendanceDate >= req.FromDate.Value.Date);
        if (req.ToDate.HasValue) query = query.Where(a => a.AttendanceDate <= req.ToDate.Value.Date);

        var records = await query.ToListAsync(ct);

        var groupedByStudent = records.GroupBy(r => r.StudentId);

        var learners = new List<AttendanceSummaryPerLearnerDto>();

        foreach (var g in groupedByStudent)
        {
            var studentId = g.Key;
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId, ct);
            var present = g.Count(r => r.Status == AttendanceStatus.Present);
            var absent = g.Count(r => r.Status == AttendanceStatus.Absent);
            var late = g.Count(r => r.Status == AttendanceStatus.Late);
            var sick = g.Count(r => r.Status == AttendanceStatus.Sick);
            var excused = g.Count(r => r.Status == AttendanceStatus.Excused);
            var total = g.Count();
            var perc = CalculatePercentage(present, late, excused, sick, absent, total, settings);
            var isChronic = perc < settings.ChronicAbsenceThreshold;
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(gr => gr.Id == g.First().GradeId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(st => st.Id == g.First().StreamId, ct);

            learners.Add(new AttendanceSummaryPerLearnerDto(
                studentId,
                student != null ? $"{student.FirstName} {student.LastName}" : $"Student {studentId}",
                student?.StudentNumber ?? "",
                g.First().GradeId,
                grade?.Name ?? "",
                g.First().StreamId,
                stream?.Name ?? "",
                total,
                present,
                absent,
                late,
                excused,
                sick,
                perc,
                isChronic,
                isChronic ? $"Chronic absence: {perc}% < threshold {settings.ChronicAbsenceThreshold}% - Flagged" : ""
            ));
        }

        var gradeInfo = req.GradeId.HasValue ? await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == req.GradeId.Value, ct) : null;
        var streamInfo = req.StreamId.HasValue ? await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == req.StreamId.Value, ct) : null;

        var avg = learners.Any() ? Math.Round(learners.Average(l => l.Percentage), 2) : 0m;
        var chronicCount = learners.Count(l => l.IsChronicAbsence);

        return new AttendanceSummaryPerClassDto(
            req.GradeId ?? 0,
            gradeInfo?.Name ?? "All Grades",
            req.StreamId ?? 0,
            streamInfo?.Name ?? "All Streams",
            req.AcademicYearId,
            req.TermId,
            "Term",
            learners.Count,
            avg,
            chronicCount,
            learners.OrderBy(l => l.Percentage).ToList()
        );
    }

    public async Task<PrintableMonthRegisterDto> GetPrintableMonthAsync(long tenantId, PrintableMonthRegisterRequest req, CancellationToken ct = default)
    {
        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == req.GradeId && g.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Grade not found");
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == req.StreamId && s.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Stream not found");

        var students = await _db.Set<Student>().Where(s => s.TenantId == tenantId && s.GradeId == req.GradeId && s.StreamId == req.StreamId && !s.IsDeleted)
            .Join(_db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.GradeId == req.GradeId && e.StreamId == req.StreamId && e.AcademicYearId == req.AcademicYearId && e.IsCurrent && !e.IsDeleted),
                s => s.Id, e => e.StudentId, (s, e) => s)
            .OrderBy(s => s.LastName).ToListAsync(ct);
        // No fallback to students by grade and stream: it listed students who had left.

        var daysInMonth = DateTime.DaysInMonth(req.Year, req.Month);
        var dates = Enumerable.Range(1, daysInMonth).Select(d => new DateTime(req.Year, req.Month, d)).ToList();

        var records = await _db.Set<AttendanceRecord>()
            .Where(a => a.TenantId == tenantId && a.GradeId == req.GradeId && a.StreamId == req.StreamId && a.AcademicYearId == req.AcademicYearId && a.TermId == req.TermId && a.AttendanceDate.Year == req.Year && a.AttendanceDate.Month == req.Month && !a.IsDeleted)
            .ToListAsync(ct);

        var rows = new List<PrintableStudentRowDto>();
        foreach (var student in students)
        {
            var dict = new Dictionary<string, string>();
            foreach (var date in dates)
            {
                var rec = records.FirstOrDefault(r => r.StudentId == student.Id && r.AttendanceDate.Date == date.Date);
                var letter = rec?.Status switch
                {
                    AttendanceStatus.Present => "P",
                    AttendanceStatus.Absent => "A",
                    AttendanceStatus.Late => "L",
                    AttendanceStatus.Sick => "S",
                    AttendanceStatus.Excused => "E",
                    _ => ""
                };
                dict[date.ToString("yyyy-MM-dd")] = letter;
            }
            rows.Add(new PrintableStudentRowDto(student.Id, $"{student.FirstName} {student.LastName}", student.StudentNumber, dict));
        }

        var monthName = new DateTime(req.Year, req.Month, 1).ToString("MMMM yyyy");

        return new PrintableMonthRegisterDto(grade.Name, stream.Name, req.Year, req.Month, monthName, dates, rows);
    }
}
