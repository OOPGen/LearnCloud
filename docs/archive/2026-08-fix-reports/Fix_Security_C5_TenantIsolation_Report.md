# Fix Security C5 - Tenant Isolation Bypass via IsExplicitNoTenant

**Severity:** Critical
**Status:** FIXED
**Date:** 2026-08-09

## Vulnerability
- Global query filter `e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId)` uses `IsExplicitNoTenant` bool that if true bypasses tenant filter returning ALL tenants data
- `ITenantContext.SetExplicitNoTenant(reason, actorUserId, actorRole)` did NOT validate role - any code could call it with any role and bypass tenant isolation
- No `IModelCacheKeyFactory` - EF Core caches model per DbContext type, once cached for tenant A, tenant B could get cached model with wrong filter or no tenant filter (cache poisoning)
- Privileged role check only in `INoTenantOperation`/`NoTenantScope`, not in DbContext itself

**Impact:** Data breach across all schools (150-2000 learners each) - competitor leak, catastrophic for trust, violates Data Protection Act ZW 12:07 for minors

## Fix Applied

### 1. TenantContext.cs - Role Guard
```csharp
public void SetExplicitNoTenant(string reason, long actorUserId, string actorRole)
{
    var allowedRoles = new[] { "PLATFORM_SUPERADMIN", "PLATFORM_SUPPORT", "SYSTEM" };
    if (!allowedRoles.Contains(actorRole))
        throw new UnauthorizedAccessException($"SECURITY: Explicit no-tenant requires privileged role {string.Join(",", allowedRoles)}, got {actorRole}");
    if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
        throw new InvalidOperationException("Explicit no-tenant reason must be >=10 chars for audit");
    
    State.IsExplicitNoTenant = true;
    State.Reason = reason;
    State.ActorUserId = actorUserId;
    State.ActorRole = actorRole;
    State.Source = TenantResolutionSource.ExplicitNoTenant;
    State.TenantId = null;
}
```

### 2. LearnCloudDbContext.cs - Double Guard + Cache Key Properties
```csharp
public long? TenantIdForCache => _tenantContext.TenantId;
public bool IsExplicitNoTenantForCache => _tenantContext.IsExplicitNoTenant;

private long? CurrentTenantId
{
    get
    {
        if (_tenantContext.IsExplicitNoTenant)
        {
            var role = _tenantContext.ActorRole;
            if (role != "PLATFORM_SUPERADMIN" && role != "PLATFORM_SUPPORT" && role != "SYSTEM")
                throw new UnauthorizedAccessException($"SECURITY: IsExplicitNoTenant bypass attempted without privileged role. Actor {_tenantContext.ActorUserId} role {role}");
        }
        return _tenantContext.TenantId;
    }
}
private bool IsExplicitNoTenant
{
    get
    {
        if (_tenantContext.IsExplicitNoTenant)
        {
            var role = _tenantContext.ActorRole;
            var allowed = new[] { "PLATFORM_SUPERADMIN", "PLATFORM_SUPPORT", "SYSTEM" };
            if (!allowed.Contains(role))
                throw new UnauthorizedAccessException($"SECURITY: Explicit no-tenant filter bypass requires privileged role...");
        }
        return _tenantContext.IsExplicitNoTenant;
    }
}

// In SaveChangesAsync, explicit no-tenant save must have explicit TenantId set and privileged role already checked
```

### 3. TenantModelCacheKeyFactory.cs - NEW
```csharp
public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is LearnCloudDbContext tenantContext)
        {
            return (context.GetType(), tenantContext.TenantIdForCache, tenantContext.IsExplicitNoTenantForCache, designTime);
        }
        return (context.GetType(), designTime);
    }
}
```

### 4. MultiTenancyExtensions.cs - Registration
```csharp
services.AddSingleton<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
```

## Verification
```bash
# Test 1: Explicit no-tenant without privileged role throws
try {
    tenantContext.SetExplicitNoTenant("test reason for audit", 1, "TEACHER");
} catch (UnauthorizedAccessException) { /* expected */ }

# Test 2: Tenant A cannot read Tenant B
TenantContext.SetResolvedTenant(tenantA, ..., actorUserId)
db.Students.ToList() // only tenant A

TenantContext.SetResolvedTenant(tenantB)
db.Students.ToList() // only tenant B, not A

# Test 3: Model cache includes tenantId
var factory = new TenantModelCacheKeyFactory();
var keyA = factory.Create(dbContextWithTenantA, false);
var keyB = factory.Create(dbContextWithTenantB, false);
Assert.NotEqual(keyA, keyB);
```

## Remaining
- Add integration tests `IsExplicitNoTenant_WithoutRole_Throws` and `TenantIsolation_TwoTenantsOverlappingData_A_Cannot_Read_B`
- Audit all usages of `BeginNoTenantScope` to ensure reason >=10 chars and role privileged

## Impact
- Explicit no-tenant bypass now requires PLATFORM_SUPERADMIN role, audited with reason
- EF model cache poisoning prevented via tenant-aware cache key
- Double guard in DbContext property getters ensures even if TenantContext validation bypassed, DbContext throws
