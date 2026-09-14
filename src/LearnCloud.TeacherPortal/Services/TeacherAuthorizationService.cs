using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.HR.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.TeacherPortal.Services;

/// <summary>
/// Authorisation is critical: every endpoint must verify teacher is assigned to class, server-side, in addition to tenant filter
/// </summary>
public interface ITeacherAuthorizationService
{
    Task<long> GetTeacherStaffIdAsync(long tenantId, long userId, CancellationToken ct = default);
    Task<bool> IsAssignedToClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, CancellationToken ct = default);
    Task EnsureAssignedToClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, CancellationToken ct = default);
    Task<List<(long gradeId, long streamId)>> GetAssignedClassesAsync(long tenantId, long teacherStaffId, CancellationToken ct = default);
    Task<bool> IsTeachingSubjectInClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, long subjectId, CancellationToken ct = default);
    Task EnsureTeachingSubjectInClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, long subjectId, CancellationToken ct = default);
}

public class TeacherAuthorizationService : ITeacherAuthorizationService
{
    private readonly LearnCloudDbContext _db;

    public TeacherAuthorizationService(LearnCloudDbContext db) => _db = db;

    public async Task<long> GetTeacherStaffIdAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        // Staff linked to user account via user_id
        var staff = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.UserId == userId && !s.IsDeleted, ct)
                    ?? throw new UnauthorizedAccessException($"User {userId} is not a teacher staff in tenant {tenantId}. Must be linked to staff profile.");
        return staff.Id;
    }

    public async Task<bool> IsAssignedToClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, CancellationToken ct = default)
    {
        // Check 1: Is class teacher of stream?
        var isClassTeacher = await _db.Set<ClassStream>().AnyAsync(s => s.TenantId == tenantId && s.Id == streamId && s.GradeId == gradeId && s.ClassTeacherStaffId == teacherStaffId && !s.IsDeleted, ct);
        if (isClassTeacher) return true;

        // Check 2: Assigned via timetable_slots for current academic year/term or any active timetable?
        // Teacher is assigned if exists timetable slot for that grade/stream where teacher_staff_id = teacherStaffId
        var isInTimetable = await _db.Set<TimetableSlot>().AnyAsync(slot =>
            slot.TenantId == tenantId && slot.TeacherStaffId == teacherStaffId && slot.GradeId == gradeId && slot.StreamId == streamId && !slot.IsDeleted, ct);

        return isInTimetable;
    }

    public async Task EnsureAssignedToClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, CancellationToken ct = default)
    {
        if (!await IsAssignedToClassAsync(tenantId, teacherStaffId, gradeId, streamId, ct))
        {
            // Log security event
            throw new UnauthorizedAccessException($"Teacher {teacherStaffId} is not assigned to class Grade {gradeId} Stream {streamId} in tenant {tenantId}. Access denied - row-level scoping enforced server-side.");
        }
    }

    public async Task<List<(long gradeId, long streamId)>> GetAssignedClassesAsync(long tenantId, long teacherStaffId, CancellationToken ct = default)
    {
        var classTeacherStreams = await _db.Set<ClassStream>()
            .Where(s => s.TenantId == tenantId && s.ClassTeacherStaffId == teacherStaffId && !s.IsDeleted)
            .Select(s => new { s.GradeId, s.Id })
            .ToListAsync(ct);

        var timetableStreams = await _db.Set<TimetableSlot>()
            .Where(slot => slot.TenantId == tenantId && slot.TeacherStaffId == teacherStaffId && !slot.IsDeleted)
            .Select(slot => new { slot.GradeId, StreamId = slot.StreamId })
            .Distinct()
            .ToListAsync(ct);

        var combined = classTeacherStreams.Select(x => (x.GradeId, x.Id))
            .Union(timetableStreams.Select(x => (x.GradeId, x.StreamId)))
            .Distinct()
            .ToList();

        return combined;
    }

    public async Task<bool> IsTeachingSubjectInClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, long subjectId, CancellationToken ct = default)
    {
        // Must be assigned to class AND teaching that subject in that class
        if (!await IsAssignedToClassAsync(tenantId, teacherStaffId, gradeId, streamId, ct))
            return false;

        // Check timetable for subject in class
        var teachesSubjectInClass = await _db.Set<TimetableSlot>().AnyAsync(slot =>
            slot.TenantId == tenantId && slot.TeacherStaffId == teacherStaffId && slot.GradeId == gradeId && slot.StreamId == streamId && slot.SubjectId == subjectId && !slot.IsDeleted, ct);

        if (teachesSubjectInClass) return true;

        // Also check staff_subjects M2M if exists
        var staffSubjects = await _db.Set<StaffSubject>().AnyAsync(ss => ss.TenantId == tenantId && ss.StaffId == teacherStaffId && ss.SubjectId == subjectId && !ss.IsDeleted, ct);
        if (staffSubjects)
        {
            // If qualified and assigned to class, allow (even if no timetable slot yet for this term)
            return true;
        }

        return false;
    }

    public async Task EnsureTeachingSubjectInClassAsync(long tenantId, long teacherStaffId, long gradeId, long streamId, long subjectId, CancellationToken ct = default)
    {
        if (!await IsTeachingSubjectInClassAsync(tenantId, teacherStaffId, gradeId, streamId, subjectId, ct))
            throw new UnauthorizedAccessException($"Teacher {teacherStaffId} does not teach subject {subjectId} in class {gradeId}/{streamId}. Marks entry and lesson plans require subject assignment.");
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade entities across 10+ namespaces - single source of truth
// Canonical entities: Student, Grade, Stream, Subject, Guardian, GuardianStudentLink, Staff, StudentEnrolment, Term, Room, SubjectGradeLink are in LearnCloud.Domain
