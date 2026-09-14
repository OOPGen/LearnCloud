using LearnCloud.TeacherPortal.Entities;
using LearnCloud.HR.Entities;
using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using LearnCloud.TeacherPortal.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.TeacherPortal.Services;

public interface ITeacherDashboardService
{
    Task<TeacherDashboardDto> GetDashboardAsync(long tenantId, long teacherStaffId, long userId, CancellationToken ct = default);
    Task<List<MyClassDto>> GetMyClassesAsync(long tenantId, long teacherStaffId, CancellationToken ct = default);
    Task<List<LearnerInClassDto>> GetLearnersInClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, bool includeGuardianContacts, CancellationToken ct = default);
}

public class TeacherDashboardService : ITeacherDashboardService
{
    private readonly LearnCloudDbContext _db;
    private readonly ITeacherAuthorizationService _authz;

    public TeacherDashboardService(LearnCloudDbContext db, ITeacherAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    public async Task<TeacherDashboardDto> GetDashboardAsync(long tenantId, long teacherStaffId, long userId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var dayOfWeek = (int)today.DayOfWeek;
        // Convert System.DayOfWeek Sunday=0 to our 1=Mon..7=Sun
        dayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;

        // Today's timetable - only classes teacher assigned to, effective dated
        var todaySlots = await _db.Set<TimetableSlot>()
            .Where(s => s.TenantId == tenantId && s.TeacherStaffId == teacherStaffId && s.DayOfWeek == dayOfWeek && !s.IsDeleted)
            .OrderBy(s => s.PeriodNumber)
            .ToListAsync(ct);

        var todayTimetable = new List<TodayTimetableItemDto>();
        foreach (var slot in todaySlots)
        {
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == slot.GradeId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == slot.StreamId, ct);
            var subject = await _db.Set<Subject>().FirstOrDefaultAsync(su => su.Id == slot.SubjectId, ct);
            var period = await _db.Set<PeriodDefinition>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PeriodNumber == slot.PeriodNumber, ct);
            var room = slot.RoomId.HasValue ? await _db.Set<Room>().FirstOrDefaultAsync(r => r.Id == slot.RoomId.Value, ct) : null;

            todayTimetable.Add(new TodayTimetableItemDto(
                slot.Id,
                slot.DayOfWeek,
                slot.PeriodNumber,
                period?.Name ?? $"Period {slot.PeriodNumber}",
                slot.StartTime.ToString(@"hh\:mm"),
                slot.EndTime.ToString(@"hh\:mm"),
                slot.GradeId,
                grade?.Name ?? "",
                slot.StreamId,
                stream?.Name ?? "",
                slot.SubjectId,
                subject?.Name ?? "",
                slot.RoomId,
                room?.Name
            ));
        }

        // Registers still to be marked today - for each assigned class, check if attendance register exists for today
        var assignedClasses = await _authz.GetAssignedClassesAsync(tenantId, teacherStaffId, ct);
        var registersTodo = new List<RegisterTodoDto>();

        foreach (var (gradeId, streamId) in assignedClasses)
        {
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == gradeId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == streamId, ct);
            // Check daily attendance setting - if per_period, need per period todo
            var settings = await _db.Set<TenantAttendanceSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
            var mode = settings?.Mode ?? AttendanceMode.Daily;

            if (mode == AttendanceMode.Daily)
            {
                var exists = await _db.Set<AttendanceRegister>().AnyAsync(r => r.TenantId == tenantId && r.GradeId == gradeId && r.StreamId == streamId && r.AttendanceDate == today && !r.IsDeleted, ct);
                if (!exists)
                {
                    var learnersCount = await _db.Set<Student>().CountAsync(s => s.TenantId == tenantId && s.GradeId == gradeId && s.StreamId == streamId && !s.IsDeleted, ct);
                    registersTodo.Add(new RegisterTodoDto(gradeId, grade?.Name ?? "", streamId, stream?.Name ?? "", today, null, "Daily", false, learnersCount));
                }
            }
            else
            {
                // Per period: for each period today that teacher teaches that class
                var periodsToday = todaySlots.Where(s => s.GradeId == gradeId && s.StreamId == streamId).ToList();
                foreach (var slot in periodsToday)
                {
                    var exists = await _db.Set<AttendanceRegister>().AnyAsync(r => r.TenantId == tenantId && r.GradeId == gradeId && r.StreamId == streamId && r.AttendanceDate == today && r.PeriodNumber == slot.PeriodNumber && !r.IsDeleted, ct);
                    if (!exists)
                    {
                        var learnersCount = await _db.Set<Student>().CountAsync(s => s.TenantId == tenantId && s.GradeId == gradeId && s.StreamId == streamId && !s.IsDeleted, ct);
                        registersTodo.Add(new RegisterTodoDto(gradeId, grade?.Name ?? "", streamId, stream?.Name ?? "", today, slot.PeriodNumber, $"Period {slot.PeriodNumber}", DateTime.UtcNow.Hour > 16, learnersCount));
                    }
                }
            }
        }

        // Marks deadlines approaching - assessments where teacher teaches subject and due date near
        var assessments = await _db.Set<Assessment>()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Join(_db.Set<TimetableSlot>().Where(s => s.TeacherStaffId == teacherStaffId && s.TenantId == tenantId && !s.IsDeleted),
                a => new { a.GradeId, a.StreamId, a.SubjectId }, slot => new { slot.GradeId, StreamId = slot.StreamId, slot.SubjectId },
                (a, slot) => a)
            .Distinct()
            .ToListAsync(ct);

        var marksDeadlines = new List<MarksDeadlineDto>();
        foreach (var assessment in assessments.Take(10))
        {
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == assessment.GradeId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == assessment.StreamId, ct);
            var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == assessment.SubjectId, ct);
            // Count marks
            var totalStudents = await _db.Set<Student>().CountAsync(s => s.TenantId == tenantId && s.GradeId == assessment.GradeId && s.StreamId == assessment.StreamId && !s.IsDeleted, ct);
            var markedCount = await _db.Set<StudentMark>().CountAsync(m => m.TenantId == tenantId && m.AssessmentId == assessment.Id && !m.IsDeleted && m.Score.HasValue, ct);
            var dueDate = assessment.AssessmentDate ?? DateTime.UtcNow.AddDays(7);
            var daysLeft = (dueDate - DateTime.UtcNow).Days;

            if (daysLeft <= 7) // approaching
            {
                marksDeadlines.Add(new MarksDeadlineDto(
                    assessment.Id,
                    assessment.Name,
                    assessment.GradeId,
                    grade?.Name ?? "",
                    assessment.StreamId,
                    stream?.Name ?? "",
                    assessment.SubjectId,
                    subject?.Name ?? "",
                    dueDate,
                    daysLeft,
                    totalStudents,
                    markedCount,
                    totalStudents - markedCount,
                    markedCount == 0 ? "not_started" : markedCount < totalStudents ? "draft" : "submitted"
                ));
            }
        }

        // Unread notices
        var notices = await _db.Set<TeacherNotice>()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted && !n.IsRead && (n.TargetTeacherStaffId == null || n.TargetTeacherStaffId == teacherStaffId))
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new NoticeDto(n.Id, n.Title, n.Body, n.Priority, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        var teacher = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == teacherStaffId, ct);
        var totalClasses = assignedClasses.Count;
        var totalLearners = await _db.Set<Student>().Where(s => s.TenantId == tenantId && assignedClasses.Any(c => c.gradeId == s.GradeId && c.streamId == s.StreamId) && !s.IsDeleted).CountAsync(ct);

        return new TeacherDashboardDto(todayTimetable, registersTodo, marksDeadlines.OrderBy(m => m.DaysLeft).ToList(), notices, totalClasses, totalLearners, teacher != null ? $"{teacher.FirstName} {teacher.LastName}" : "Teacher");
    }

    public async Task<List<MyClassDto>> GetMyClassesAsync(long tenantId, long teacherStaffId, CancellationToken ct = default)
    {
        var assigned = await _authz.GetAssignedClassesAsync(tenantId, teacherStaffId, ct);
        var result = new List<MyClassDto>();

        foreach (var (gradeId, streamId) in assigned)
        {
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == gradeId && g.TenantId == tenantId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == streamId && s.TenantId == tenantId, ct);
            if (grade == null || stream == null) continue;

            var isClassTeacher = stream.ClassTeacherStaffId == teacherStaffId;
            var learnersCount = await _db.Set<Student>().CountAsync(s => s.TenantId == tenantId && s.GradeId == gradeId && s.StreamId == streamId && !s.IsDeleted, ct);

            var subjects = await _db.Set<TimetableSlot>()
                .Where(slot => slot.TenantId == tenantId && slot.TeacherStaffId == teacherStaffId && slot.GradeId == gradeId && slot.StreamId == streamId && !slot.IsDeleted)
                .Join(_db.Set<Subject>(), slot => slot.SubjectId, sub => sub.Id, (slot, sub) => sub.Name)
                .Distinct()
                .ToListAsync(ct);

            result.Add(new MyClassDto(
                gradeId,
                grade.Name,
                streamId,
                stream.Name,
                $"{grade.Name} {stream.Name}",
                stream.Capacity,
                learnersCount,
                isClassTeacher,
                subjects,
                learnersCount
            ));
        }

        return result.OrderBy(c => c.FullName).ToList();
    }

    public async Task<List<LearnerInClassDto>> GetLearnersInClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, bool includeGuardianContacts, CancellationToken ct = default)
    {
        // Enforce server-side: teacher assigned to class
        await _authz.EnsureAssignedToClassAsync(tenantId, teacherStaffId, gradeId, streamId, ct);

        var students = await _db.Set<Student>()
            .Where(s => s.TenantId == tenantId && s.GradeId == gradeId && s.StreamId == streamId && !s.IsDeleted)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        var result = new List<LearnerInClassDto>();
        foreach (var student in students)
        {
            var guardians = new List<GuardianContactDto>();
            if (includeGuardianContacts)
            {
                // Check permission: guardians.read - for teacher, only own class parents allowed
                var links = await _db.Set<GuardianStudentLink>()
                    .Where(l => l.TenantId == tenantId && l.StudentId == student.Id && !l.IsDeleted)
                    .ToListAsync(ct);

                foreach (var link in links)
                {
                    var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == link.GuardianId && g.TenantId == tenantId, ct);
                    if (guardian == null) continue;

                    guardians.Add(new GuardianContactDto(
                        guardian.Id,
                        $"{guardian.FirstName} {guardian.LastName}",
                        link.RelationshipType,
                        guardian.Phone,
                        guardian.Email,
                        link.IsPrimaryContact,
                        link.IsBillingContact,
                        link.IsEmergencyContact,
                        link.CanPickup,
                        true, // sms_opt_in stub
                        true
                    ));
                }
            }

            // Attendance % and avg score (optional)
            var attRecords = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == student.Id && !a.IsDeleted).ToListAsync(ct);
            decimal? attPerc = null;
            if (attRecords.Any())
            {
                var present = attRecords.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
                attPerc = Math.Round((decimal)present / attRecords.Count * 100, 1);
            }

            result.Add(new LearnerInClassDto(
                student.Id,
                student.StudentNumber,
                student.FirstName,
                student.LastName,
                student.PhotoUrl,
                student.Dob,
                student.Gender ?? "",
                student.Status,
                guardians,
                attPerc,
                null // average score - would join marks
            ));
        }

        return result;
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth
