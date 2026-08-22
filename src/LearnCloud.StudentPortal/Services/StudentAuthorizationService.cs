using Microsoft.EntityFrameworkCore;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.StudentPortal.Services;

/// <summary>
/// Scope every endpoint to authenticated student's own record, verified server-side
/// </summary>
public interface IStudentAuthorizationService
{
    Task<long> GetStudentIdAsync(long tenantId, long userId, CancellationToken ct = default);
    Task<bool> IsOwnRecordAsync(long tenantId, long studentId, long authenticatedStudentId, CancellationToken ct = default);
    Task EnsureOwnRecordAsync(long tenantId, long studentId, long authenticatedStudentId, CancellationToken ct = default);
}

public class StudentAuthorizationService : IStudentAuthorizationService
{
    private readonly LearnCloudDbContext _db;

    public StudentAuthorizationService(LearnCloudDbContext db) => _db = db;

    public async Task<long> GetStudentIdAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        // Student linked to user via user_id column (added via migration)
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.UserId == userId && !s.IsDeleted, ct)
                      ?? throw new UnauthorizedAccessException($"User {userId} is not a student in tenant {tenantId}. No student link found. Access denied.");
        return student.Id;
    }

    public async Task<bool> IsOwnRecordAsync(long tenantId, long studentId, long authenticatedStudentId, CancellationToken ct = default)
    {
        // Only own record allowed
        return studentId == authenticatedStudentId;
    }

    public async Task EnsureOwnRecordAsync(long tenantId, long studentId, long authenticatedStudentId, CancellationToken ct = default)
    {
        if (studentId != authenticatedStudentId)
        {
            throw new UnauthorizedAccessException($"Student {authenticatedStudentId} attempted to access student {studentId} in tenant {tenantId}. Access denied - own record scoping enforced server-side.");
        }

        // Double check tenant filter: student must belong to tenant
        var exists = await _db.Set<Student>().AnyAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct);
        if (!exists)
        {
            throw new UnauthorizedAccessException($"Student {studentId} not found in tenant {tenantId} or tenant filter blocked. Possible ID guessing attempt.");
        }
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade entities across 10+ namespaces - single source of truth
// Canonical entities: Student, Grade, Stream, Subject, Guardian, GuardianStudentLink, Staff, StudentEnrolment, Term, Room, SubjectGradeLink are in LearnCloud.Domain