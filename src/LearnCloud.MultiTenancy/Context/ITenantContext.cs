using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.MultiTenancy.Context;

public enum TenantResolutionSource
{
    None = 0,
    Subdomain = 1,      // anonymous login context - purely to select branding
    JwtToken = 2,       // authenticated - source of truth from validated JWT claim tid
    ExplicitNoTenant = 3, // platform admin / background job explicit scope
    Header = 4          // X-Tenant-Id header for internal services (optional)
}

public interface ITenantContext
{
    long? TenantId { get; } // null = platform or not resolved
    long? SubdomainTenantId { get; }
    long? TokenTenantId { get; }
    Tenant? CurrentTenant { get; }
    bool IsResolved { get; }
    bool IsPlatform { get; } // tenant_id null but authenticated as platform
    bool IsExplicitNoTenant { get; }
    TenantResolutionSource ResolutionSource { get; }
    string? ResolutionReason { get; } // for auditing explicit no-tenant
    long? ActorUserId { get; }
    string? ActorRole { get; }

    // For tenant-less operations explicit
    IDisposable BeginNoTenantScope(string reason, long actorUserId, string actorRole = "PLATFORM_SUPERADMIN");
    IDisposable BeginTenantScope(long tenantId, TenantResolutionSource source = TenantResolutionSource.JwtToken);

    // Internal setters used by middleware
    void SetResolvedTenant(long tenantId, Tenant tenant, TenantResolutionSource source, long? subdomainTenantId = null, long? tokenTenantId = null, long? actorUserId = null);
    void SetSubdomainTenant(long? subdomainTenantId, Tenant? tenant);
    void SetActor(long actorUserId, string? actorRole);
    void SetExplicitNoTenant(string reason, long actorUserId, string actorRole);
    void Clear();
}
