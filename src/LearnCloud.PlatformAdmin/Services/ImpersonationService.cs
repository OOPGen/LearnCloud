using Role = LearnCloud.Auth.Entities.Role;
using UserRole = LearnCloud.Auth.Entities.UserRole;
using User = LearnCloud.Auth.Entities.User;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformAdmin.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformAdmin.Services;

public interface IImpersonationService
{
    // School admin grants access - only SCHOOL_ADMIN can create grant, platform admin cannot create grant for themselves by design
    Task<ImpersonationGrant> GrantAccessAsync(long tenantId, long grantedByUserId, string reason, int durationMinutes, bool hasConsent, CancellationToken ct = default);
    Task<List<ImpersonationGrant>> ListGrantsAsync(long tenantId, CancellationToken ct = default);
    Task<ImpersonationGrant> RevokeGrantAsync(long tenantId, long grantId, long revokedByUserId, CancellationToken ct = default);

    // Platform admin starts session using existing grant - impersonation without consent impossible by design
    Task<ImpersonationSession> StartImpersonationAsync(long tenantId, long grantId, long impersonatorUserId, string ip, string? userAgent, CancellationToken ct = default);
    Task EndImpersonationAsync(long sessionId, long impersonatorUserId, CancellationToken ct = default);
    Task<List<ImpersonationSession>> ListActiveSessionsAsync(long? tenantId, CancellationToken ct = default);
    Task<bool> IsImpersonatingAsync(long impersonatorUserId, CancellationToken ct = default);
    Task<ImpersonationSession?> GetCurrentSessionAsync(long impersonatorUserId, CancellationToken ct = default);
}

public class ImpersonationService : IImpersonationService
{
    private readonly LearnCloudDbContext _db;
    private readonly ILogger<ImpersonationService> _logger;

    public ImpersonationService(LearnCloudDbContext db, ILogger<ImpersonationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ImpersonationGrant> GrantAccessAsync(long tenantId, long grantedByUserId, string reason, int durationMinutes, bool hasConsent, CancellationToken ct = default)
    {
        // Enforce that grantedByUserId must be a school admin in that tenant, not platform admin - by design
        var grantedByUser = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == grantedByUserId && !u.IsDeleted, ct);
        if (grantedByUser == null) throw new InvalidOperationException("Granting user not found");

        // Check role: must be SCHOOL_ADMIN in tenant
        var isSchoolAdmin = await _db.Set<UserRole>().Join(_db.Set<Role>(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
            .AnyAsync(x => x.ur.UserId == grantedByUserId && x.ur.TenantId == tenantId && x.r.Code == "SCHOOL_ADMIN" && !x.ur.IsDeleted, ct);

        if (!isSchoolAdmin)
        {
            _logger.LogCritical("Impersonation grant attempt by non-school-admin user {UserId} tenant {TenantId} - blocked by design", grantedByUserId, tenantId);
            throw new UnauthorizedAccessException("Only school admin (SCHOOL_ADMIN role) in that tenant can grant impersonation access. Platform admin cannot grant for themselves by design.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new InvalidOperationException("Reason >=10 chars required for audit");
        if (!hasConsent)
            throw new InvalidOperationException("Explicit consent checkbox required");

        if (durationMinutes < 5 || durationMinutes > 240)
            throw new InvalidOperationException("Duration must be 5-240 minutes (max 4 hours)");

        var grant = new ImpersonationGrant
        {
            TenantId = tenantId,
            GrantedByUserId = grantedByUserId,
            GrantedByRole = "SCHOOL_ADMIN",
            GrantedToRole = "PLATFORM_SUPERADMIN",
            HasConsent = hasConsent,
            Reason = reason,
            ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes),
            CreatedBy = grantedByUserId
        };

        // Generate token hash for verification (raw token not stored)
        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLower();
        grant.TokenHash = tokenHash;

        _db.Set<ImpersonationGrant>().Add(grant);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = grantedByUserId,
            EntityType = "ImpersonationGrant",
            EntityId = 0, // will be updated after save
            Action = "grant_impersonation",
            NewValues = $"{{\"reason\":\"{reason}\",\"durationMinutes\":{durationMinutes},\"hasConsent\":{hasConsent.ToString().ToLower()},\"expiresAt\":\"{grant.ExpiresAt:o}\"}}",
            CreatedBy = grantedByUserId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Impersonation grant {GrantId} created by school admin {GrantedBy} for tenant {TenantId} reason {Reason} expires {ExpiresAt}", grant.Id, grantedByUserId, tenantId, reason, grant.ExpiresAt);

        return grant;
    }

    public async Task<List<ImpersonationGrant>> ListGrantsAsync(long tenantId, CancellationToken ct = default)
    {
        return await _db.Set<ImpersonationGrant>().Where(g => g.TenantId == tenantId && !g.IsDeleted).OrderByDescending(g => g.CreatedAt).ToListAsync(ct);
    }

    public async Task<ImpersonationGrant> RevokeGrantAsync(long tenantId, long grantId, long revokedByUserId, CancellationToken ct = default)
    {
        var grant = await _db.Set<ImpersonationGrant>().FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId && !g.IsDeleted, ct)
                    ?? throw new InvalidOperationException("Grant not found");

        grant.RevokedAt = DateTime.UtcNow;
        grant.RevokedByUserId = revokedByUserId;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = revokedByUserId,
            EntityType = "ImpersonationGrant",
            EntityId = grant.Id,
            Action = "revoke_impersonation_grant",
            OldValues = $"{{\"isActive\":true}}",
            NewValues = $"{{\"isActive\":false,\"revokedAt\":\"{grant.RevokedAt:o}\"}}",
            CreatedBy = revokedByUserId
        });

        await _db.SaveChangesAsync(ct);
        return grant;
    }

    public async Task<ImpersonationSession> StartImpersonationAsync(long tenantId, long grantId, long impersonatorUserId, string ip, string? userAgent, CancellationToken ct = default)
    {
        // Verify impersonator is PLATFORM_SUPERADMIN
        var impersonator = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == impersonatorUserId && !u.IsDeleted, ct);
        if (impersonator == null) throw new InvalidOperationException("Impersonator not found");

        var isPlatformSuperAdmin = await _db.Set<UserRole>().Join(_db.Set<Role>(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
            .AnyAsync(x => x.ur.UserId == impersonatorUserId && x.r.Code == "PLATFORM_SUPERADMIN" && !x.ur.IsDeleted, ct);

        if (!isPlatformSuperAdmin)
            throw new UnauthorizedAccessException("Only PLATFORM_SUPERADMIN can start impersonation session");

        // Check grant exists, is active, has consent, not expired, not revoked, and was created by tenant admin (not platform admin) - enforced by GrantAccessAsync
        var grant = await _db.Set<ImpersonationGrant>().FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId && !g.IsDeleted, ct)
                    ?? throw new InvalidOperationException("Grant not found");

        if (!grant.IsActive)
            throw new InvalidOperationException($"Grant not active: HasConsent={grant.HasConsent}, ExpiresAt={grant.ExpiresAt}, RevokedAt={grant.RevokedAt}, IsDeleted={grant.IsDeleted}. Impersonation without consent impossible by design.");

        // Impersonation without consent impossible by design: if grant was not created by SCHOOL_ADMIN, or HasConsent false, IsActive false, so cannot start session

        var session = new ImpersonationSession
        {
            TenantId = tenantId,
            GrantId = grantId,
            ImpersonatorUserId = impersonatorUserId,
            ImpersonatedUserId = grant.GrantedByUserId, // impersonate as school admin who granted? Or as tenant generally
            StartedAt = DateTime.UtcNow,
            ExpiresAt = grant.ExpiresAt, // session expires automatically when grant expires
            BannerMessage = $"You are in support impersonation mode for tenant {tenantId} - granted by school admin {grant.GrantedByUserId} reason: {grant.Reason} - expires {grant.ExpiresAt:u} - all actions audited as performed-on-behalf-of",
            IpAddress = ip,
            UserAgent = userAgent,
            CreatedBy = impersonatorUserId
        };

        _db.Set<ImpersonationSession>().Add(session);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = impersonatorUserId,
            EntityType = "ImpersonationSession",
            EntityId = 0,
            Action = "start_impersonation",
            NewValues = $"{{\"grantId\":{grantId},\"tenantId\":{tenantId},\"expiresAt\":\"{session.ExpiresAt:o}\",\"reason\":\"{grant.Reason}\"}}",
            CreatedBy = impersonatorUserId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogCritical("Impersonation session {SessionId} started by platform admin {Impersonator} for tenant {TenantId} grant {GrantId} expires {ExpiresAt} IP {Ip}", session.Id, impersonatorUserId, tenantId, grantId, session.ExpiresAt, ip);

        return session;
    }

    public async Task EndImpersonationAsync(long sessionId, long impersonatorUserId, CancellationToken ct = default)
    {
        var session = await _db.Set<ImpersonationSession>().FirstOrDefaultAsync(s => s.Id == sessionId && s.ImpersonatorUserId == impersonatorUserId && !s.IsDeleted, ct)
                      ?? throw new InvalidOperationException("Session not found");

        session.EndedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = session.TenantId,
            UserId = impersonatorUserId,
            EntityType = "ImpersonationSession",
            EntityId = session.Id,
            Action = "end_impersonation",
            CreatedBy = impersonatorUserId
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ImpersonationSession>> ListActiveSessionsAsync(long? tenantId, CancellationToken ct = default)
    {
        var query = _db.Set<ImpersonationSession>().Where(s => !s.IsDeleted && s.EndedAt == null && s.ExpiresAt > DateTime.UtcNow).AsQueryable();
        if (tenantId.HasValue) query = query.Where(s => s.TenantId == tenantId.Value);
        return await query.OrderByDescending(s => s.StartedAt).ToListAsync(ct);
    }

    public async Task<bool> IsImpersonatingAsync(long impersonatorUserId, CancellationToken ct = default)
    {
        return await _db.Set<ImpersonationSession>().AnyAsync(s => s.ImpersonatorUserId == impersonatorUserId && s.EndedAt == null && s.ExpiresAt > DateTime.UtcNow && !s.IsDeleted, ct);
    }

    public async Task<ImpersonationSession?> GetCurrentSessionAsync(long impersonatorUserId, CancellationToken ct = default)
    {
        return await _db.Set<ImpersonationSession>().Where(s => s.ImpersonatorUserId == impersonatorUserId && s.EndedAt == null && s.ExpiresAt > DateTime.UtcNow && !s.IsDeleted).OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync(ct);
    }
}
