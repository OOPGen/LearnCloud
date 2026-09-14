using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.HR.Entities;
using User = LearnCloud.Auth.Entities.User;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.StudentPortal.DTOs;
using LearnCloud.StudentPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.StudentPortal.Services;

public interface IStudentPortalService
{
    Task<StudentDashboardDto> GetDashboardAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<List<TimetableDayDto>> GetTimetableWeekAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<List<AttendanceRecordDto>> GetAttendanceAsync(long tenantId, long studentId, long? academicYearId, long? termId, CancellationToken ct = default);
    Task<AttendanceSummaryDto> GetAttendanceSummaryAsync(long tenantId, long studentId, long academicYearId, long termId, CancellationToken ct = default);
    Task<List<ReportCardDto>> GetPublishedResultsAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<ReportCardDetailDto> GetReportCardDetailAsync(long tenantId, long studentId, long reportCardId, CancellationToken ct = default);
    Task<List<AssignmentDto>> GetAssignmentsAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<AssignmentDto> SubmitAssignmentAsync(long tenantId, long studentId, long assignmentId, SubmitAssignmentRequest req, long userId, CancellationToken ct = default);
    Task<List<NoticeDto>> GetNoticesAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<FeeSummaryDto?> GetFeeSummaryAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<StudentProfileDto> GetProfileAsync(long tenantId, long studentId, long userId, CancellationToken ct = default);
    Task<StudentProfileDto> UpdateProfileAsync(long tenantId, long studentId, long userId, UpdateStudentProfileRequest req, CancellationToken ct = default);
}

public class StudentPortalService : IStudentPortalService
{
    private readonly LearnCloudDbContext _db;
    private readonly IStudentAuthorizationService _authz;

    public StudentPortalService(LearnCloudDbContext db, IStudentAuthorizationService authz) { _db = db; _authz = authz; }

    public async Task<StudentDashboardDto> GetDashboardAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");
        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);

        var timetable = await GetTimetableWeekAsync(tenantId, studentId, ct);
        var attendance = await GetAttendanceSummaryAsync(tenantId, studentId, student.AcademicYearId, 1, ct); // term 1 default, real would use current term
        var latestResult = await GetPublishedResultsAsync(tenantId, studentId, ct);
        var latest = latestResult.FirstOrDefault();
        LatestResultDto? latestDto = latest != null ? new LatestResultDto(latest.Id, latest.TermName, latest.Average, latest.OverallGrade, latest.PublishedAt) : null;

        var assignments = await GetAssignmentsAsync(tenantId, studentId, ct);
        var upcoming = assignments.Where(a => a.DaysLeft >= 0 && a.DaysLeft <= 14).Take(5).ToList();

        var notices = await GetNoticesAsync(tenantId, studentId, ct);
        var feeSummary = await GetFeeSummaryAsync(tenantId, studentId, ct);

        bool canViewFees = feeSummary != null;

        return new StudentDashboardDto(
            studentId,
            $"{student.FirstName} {student.LastName}",
            student.StudentNumber,
            grade?.Name ?? "",
            stream?.Name ?? "",
            timetable,
            attendance,
            latestDto,
            upcoming,
            notices.Take(3).ToList(),
            feeSummary,
            canViewFees
        );
    }

    public async Task<List<TimetableDayDto>> GetTimetableWeekAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");

        // Find effective timetable for student's grade/stream, today
        var today = DateTime.UtcNow.Date;
        var timetable = await _db.Set<Timetable>().Where(t => t.TenantId == tenantId && t.AcademicYearId == student.AcademicYearId && t.Status == "active" && !t.IsDeleted && t.EffectiveFrom <= today && (t.EffectiveTo == null || t.EffectiveTo >= today)).OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);

        if (timetable == null)
        {
            // Return empty week
            return Enumerable.Range(1,5).Select(d => new TimetableDayDto(d, DayName(d), new List<TimetableSlotDto>())).ToList();
        }

        var slots = await _db.Set<TimetableSlot>().Where(s => s.TimetableId == timetable.Id && s.GradeId == student.GradeId && s.StreamId == student.StreamId && s.TenantId == tenantId && !s.IsDeleted).OrderBy(s => s.DayOfWeek).ThenBy(s => s.PeriodNumber).ToListAsync(ct);

        var result = new List<TimetableDayDto>();
        for (int day = 1; day <= 5; day++) // Mon-Fri
        {
            var daySlots = slots.Where(s => s.DayOfWeek == day).ToList();
            var slotDtos = new List<TimetableSlotDto>();
            foreach (var slot in daySlots)
            {
                var subject = await _db.Set<Subject>().FirstOrDefaultAsync(su => su.Id == slot.SubjectId, ct);
                var teacher = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == slot.TeacherStaffId, ct);
                var room = slot.RoomId.HasValue ? await _db.Set<Room>().FirstOrDefaultAsync(r => r.Id == slot.RoomId.Value, ct) : null;
                var period = await _db.Set<PeriodDefinition>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PeriodNumber == slot.PeriodNumber, ct);

                slotDtos.Add(new TimetableSlotDto(
                    slot.PeriodNumber,
                    period?.Name ?? $"Period {slot.PeriodNumber}",
                    slot.StartTime.ToString(@"hh\:mm"),
                    slot.EndTime.ToString(@"hh\:mm"),
                    subject?.Name ?? "",
                    teacher != null ? $"{teacher.FirstName} {teacher.LastName}" : "",
                    room?.Name,
                    period?.IsBreak ?? false
                ));
            }
            result.Add(new TimetableDayDto(day, DayName(day), slotDtos));
        }

        return result;
    }

    public async Task<List<AttendanceRecordDto>> GetAttendanceAsync(long tenantId, long studentId, long? academicYearId, long? termId, CancellationToken ct = default)
    {
        var query = _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == studentId && !a.IsDeleted);
        if (academicYearId.HasValue) query = query.Where(a => a.AcademicYearId == academicYearId.Value);
        if (termId.HasValue) query = query.Where(a => a.TermId == termId.Value);

        var records = await query.OrderByDescending(a => a.AttendanceDate).Take(100).ToListAsync(ct);
        var result = new List<AttendanceRecordDto>();
        foreach (var r in records)
        {
            var period = r.PeriodNumber.HasValue ? await _db.Set<PeriodDefinition>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PeriodNumber == r.PeriodNumber.Value, ct) : null;
            result.Add(new AttendanceRecordDto(r.AttendanceDate, r.Status.ToString(), r.AbsenceReason, r.Note, r.PeriodNumber, period?.Name ?? (r.PeriodNumber.HasValue ? $"Period {r.PeriodNumber}" : "Daily")));
        }
        return result;
    }

    public async Task<AttendanceSummaryDto> GetAttendanceSummaryAsync(long tenantId, long studentId, long academicYearId, long termId, CancellationToken ct = default)
    {
        var records = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == studentId && a.AcademicYearId == academicYearId && a.TermId == termId && !a.IsDeleted).ToListAsync(ct);
        var total = records.Count;
        var present = records.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late || r.Status == AttendanceStatus.Excused);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var late = records.Count(r => r.Status == AttendanceStatus.Late);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
        var sick = records.Count(r => r.Status == AttendanceStatus.Sick);
        var perc = total > 0 ? Math.Round((decimal)present / total * 100, 1) : 0m;
        return new AttendanceSummaryDto(total, present, absent, late, excused, perc, perc < 85m && total > 10);
    }

    public async Task<List<ReportCardDto>> GetPublishedResultsAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        // Only published visible
        var cards = await _db.Set<ReportCard>().Where(rc => rc.TenantId == tenantId && rc.StudentId == studentId && rc.Status == "published" && !rc.IsDeleted).OrderByDescending(rc => rc.PublishedAt).ToListAsync(ct);
        var result = new List<ReportCardDto>();
        foreach (var rc in cards)
        {
            var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.Id == rc.TermId, ct);
            result.Add(new ReportCardDto(rc.Id, term?.Name ?? $"Term {rc.TermId}", rc.TotalAverage, rc.OverallGradeLetter, rc.ClassRank, rc.PublishedAt ?? rc.CreatedAt, rc.PdfUrl, rc.Status));
        }
        return result;
    }

    public async Task<ReportCardDetailDto> GetReportCardDetailAsync(long tenantId, long studentId, long reportCardId, CancellationToken ct = default)
    {
        var rc = await _db.Set<ReportCard>().FirstOrDefaultAsync(r => r.Id == reportCardId && r.TenantId == tenantId && r.StudentId == studentId && !r.IsDeleted, ct) ?? throw new InvalidOperationException("Report card not found");
        if (rc.Status != "published") throw new UnauthorizedAccessException("Report card not published");

        var subjects = await _db.Set<ReportCardSubject>().Where(s => s.ReportCardId == rc.Id && !s.IsDeleted).ToListAsync(ct);
        var subjectDtos = subjects.Select(s => new SubjectResultDto(s.SubjectName, s.Score, s.MaxScore, s.GradeLetter, s.TeacherComment)).ToList();

        var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.Id == rc.TermId, ct);
        var header = new ReportCardDto(rc.Id, term?.Name ?? $"Term {rc.TermId}", rc.TotalAverage, rc.OverallGradeLetter, rc.ClassRank, rc.PublishedAt ?? rc.CreatedAt, rc.PdfUrl, rc.Status);

        return new ReportCardDetailDto(header, subjectDtos);
    }

    public async Task<List<AssignmentDto>> GetAssignmentsAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");

        var assignments = await _db.Set<TeacherPortal.Entities.HomeworkAssignment>().Where(h => h.TenantId == tenantId && h.GradeId == student.GradeId && h.StreamId == student.StreamId && !h.IsDeleted && h.DueDate >= DateTime.UtcNow.Date.AddDays(-7)).OrderBy(h => h.DueDate).Take(20).ToListAsync(ct);

        var result = new List<AssignmentDto>();
        foreach (var hw in assignments)
        {
            var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == hw.SubjectId, ct);
            var submission = await _db.Set<StudentAssignmentSubmission>().FirstOrDefaultAsync(s => s.AssignmentId == hw.Id && s.StudentId == studentId && s.TenantId == tenantId && !s.IsDeleted, ct);
            var status = submission?.Status ?? "pending";
            var canSubmit = true; // if school setting allows
            var settings = await _db.Set<StudentPortalSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
            if (settings != null) canSubmit = settings.AllowStudentsSubmitAssignments;

            result.Add(new AssignmentDto(hw.Id, hw.Title, subject?.Name ?? "", hw.DueDate, (hw.DueDate - DateTime.UtcNow.Date).Days, status, canSubmit, submission?.FileUrl));
        }

        return result;
    }

    public async Task<AssignmentDto> SubmitAssignmentAsync(long tenantId, long studentId, long assignmentId, SubmitAssignmentRequest req, long userId, CancellationToken ct = default)
    {
        var settings = await _db.Set<StudentPortalSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (settings != null && !settings.AllowStudentsSubmitAssignments)
            throw new InvalidOperationException("Assignment submission disabled by school");

        var assignment = await _db.Set<TeacherPortal.Entities.HomeworkAssignment>().FirstOrDefaultAsync(h => h.Id == assignmentId && h.TenantId == tenantId && !h.IsDeleted, ct) ?? throw new InvalidOperationException("Assignment not found");

        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");
        if (assignment.GradeId != student.GradeId || assignment.StreamId != student.StreamId)
            throw new UnauthorizedAccessException("Assignment not for your class");

        var existing = await _db.Set<StudentAssignmentSubmission>().FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId && s.TenantId == tenantId && !s.IsDeleted, ct);
        if (existing == null)
        {
            existing = new StudentAssignmentSubmission
            {
                TenantId = tenantId,
                AssignmentId = assignmentId,
                StudentId = studentId,
                Status = DateTime.UtcNow.Date > assignment.DueDate ? "late" : "submitted",
                SubmittedAt = DateTime.UtcNow,
                FileUrl = req.FileUrl,
                FileName = req.FileName,
                Note = req.Note,
                CreatedBy = userId
            };
            _db.Set<StudentAssignmentSubmission>().Add(existing);
        }
        else
        {
            existing.Status = DateTime.UtcNow.Date > assignment.DueDate ? "late" : "submitted";
            existing.SubmittedAt = DateTime.UtcNow;
            existing.FileUrl = req.FileUrl ?? existing.FileUrl;
            existing.FileName = req.FileName ?? existing.FileName;
            existing.Note = req.Note ?? existing.Note;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == assignment.SubjectId, ct);
        return new AssignmentDto(assignment.Id, assignment.Title, subject?.Name ?? "", assignment.DueDate, (assignment.DueDate - DateTime.UtcNow.Date).Days, existing.Status, true, existing.FileUrl);
    }

    public async Task<List<NoticeDto>> GetNoticesAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");

        // Notices for student's class - message batches
        var batches = await _db.Set<Messaging.Entities.MessageBatch>().Where(b => b.TenantId == tenantId && !b.IsDeleted).OrderByDescending(b => b.CreatedAt).Take(20).ToListAsync(ct);
        // Filter by audience: if audience contains grade/stream or all
        var result = new List<NoticeDto>();
        foreach (var b in batches)
        {
            // Simplified: include all for demo, real would check audience_filter_json
            result.Add(new NoticeDto(b.Id, b.Title, b.Body, b.CreatedAt, "normal"));
        }
        return result;
    }

    public async Task<FeeSummaryDto?> GetFeeSummaryAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        var settings = await _db.Set<StudentPortalSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (settings != null && !settings.AllowStudentsViewFees)
        {
            return null; // School-level setting controlling whether students may see fee information at all
        }

        var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && !i.IsDeleted).ToListAsync(ct);
        var totalInvoiced = invoices.Sum(i => i.TotalAmount);
        var totalPaid = invoices.Sum(i => i.AmountPaid);
        var balance = invoices.Sum(i => i.BalanceDue);

        return new FeeSummaryDto(balance, invoices.FirstOrDefault()?.Currency ?? "USD", totalInvoiced, totalPaid, true);
    }

    public async Task<StudentProfileDto> GetProfileAsync(long tenantId, long studentId, long userId, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");
        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);
        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

        return new StudentProfileDto(student.Id, userId, student.StudentNumber, student.FirstName, student.LastName, $"{student.FirstName} {student.LastName}", user?.Email, user?.Phone, grade?.Name ?? "", stream?.Name ?? "", student.PhotoUrl, student.Dob, student.Status);
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(long tenantId, long studentId, long userId, UpdateStudentProfileRequest req, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");
        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

        if (user != null)
        {
            if (!string.IsNullOrWhiteSpace(req.Phone)) user.Phone = req.Phone;
            if (!string.IsNullOrWhiteSpace(req.Email)) user.Email = req.Email.ToLower();
            user.UpdatedBy = userId;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await GetProfileAsync(tenantId, studentId, userId, ct);
    }

    private static string DayName(int day) => day switch { 1 => "Monday", 2 => "Tuesday", 3 => "Wednesday", 4 => "Thursday", 5 => "Friday", 6 => "Saturday", 7 => "Sunday", _ => $"Day {day}" };
}

// Entities from AttendanceTimetable, Auth and Domain were previously stubbed here.
// They are now referenced from their owning modules.
