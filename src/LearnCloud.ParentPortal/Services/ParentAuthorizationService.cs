using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.ParentPortal.Services;

/// <summary>
/// Every endpoint verifies guardian-child link server-side, in addition to tenant filter
/// </summary>
public interface IParentAuthorizationService
{
    Task<long> GetGuardianIdAsync(long tenantId, long userId, CancellationToken ct = default);
    Task<bool> IsGuardianOfStudentAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task EnsureGuardianOfStudentAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<List<long>> GetStudentIdsForGuardianAsync(long tenantId, long guardianId, CancellationToken ct = default);
    Task<List<long>> GetGuardianIdsForStudentAsync(long tenantId, long studentId, CancellationToken ct = default);
}

public class ParentAuthorizationService : IParentAuthorizationService
{
    private readonly LearnCloudDbContext _db;

    public ParentAuthorizationService(LearnCloudDbContext db) => _db = db;

    public async Task<long> GetGuardianIdAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        // Guardian linked to user via user_id
        var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.TenantId == tenantId && g.UserId == userId && !g.IsDeleted, ct)
                       ?? throw new UnauthorizedAccessException($"User {userId} is not a guardian in tenant {tenantId}. No guardian link found. Access denied.");
        return guardian.Id;
    }

    public async Task<bool> IsGuardianOfStudentAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        return await _db.Set<GuardianStudentLink>().AnyAsync(l => l.TenantId == tenantId && l.GuardianId == guardianId && l.StudentId == studentId && !l.IsDeleted, ct);
    }

    public async Task EnsureGuardianOfStudentAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        if (!await IsGuardianOfStudentAsync(tenantId, guardianId, studentId, ct))
        {
            throw new UnauthorizedAccessException($"Guardian {guardianId} is not linked to student {studentId} in tenant {tenantId}. Access denied - guardian-child link enforced server-side.");
        }
    }

    public async Task<List<long>> GetStudentIdsForGuardianAsync(long tenantId, long guardianId, CancellationToken ct = default)
    {
        return await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.GuardianId == guardianId && !l.IsDeleted).Select(l => l.StudentId).Distinct().ToListAsync(ct);
    }

    public async Task<List<long>> GetGuardianIdsForStudentAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        return await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == studentId && !l.IsDeleted).Select(l => l.GuardianId).Distinct().ToListAsync(ct);
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade entities across 10+ namespaces - single source of truth
// Canonical entities: Student, Grade, Stream, Subject, Guardian, GuardianStudentLink, Staff, StudentEnrolment, Term, Room, SubjectGradeLink are in LearnCloud.Domain