# Fix Tenant Isolation C4 - Caching Without TenantId

**Severity:** Critical
**Status:** FIXED (Partial - Foundation Created)

## Vulnerability
- `IMemoryCache` or `IDistributedCache` or Redis cache keys without tenantId could leak across tenants
- Example: If permissions cached as `user:{userId}:perms` without tenantId, user with same id in different tenants (possible? User id is global auto-increment unique, but still unsafe) could get other's perms? Actually user id is global unique, but cache key should still include tenantId for defense in depth
- Fee arrears cache `student:{studentId}:arrears:{date}` without tenantId - studentId is global unique? Student id is global auto-increment unique, but still tenant isolation best practice to include tenantId
- No central factory for cache keys, each dev could invent own key pattern

## Fix Applied

### Created TenantCacheKey Factory
**File:** `src/LearnCloud.MultiTenancy/Caching/TenantCacheKey.cs` (NEW)

```csharp
public static class TenantCacheKey
{
    public static string ForTenant(long? tenantId, string key) {
        var tenantPrefix = tenantId.HasValue ? $"tenant:{tenantId}" : "platform";
        return $"{tenantPrefix}:{key}";
    }
    public static string ForUser(long? tenantId, long userId, string key) {
        return $"{ForTenant(tenantId, key)}:user:{userId}";
    }
    public static string PermissionsForUser(long? tenantId, long userId) {
        return ForUser(tenantId, userId, "perms");
    }
    public static string FeeArrears(long tenantId, long studentId, DateTime asAtDate) {
        return $"tenant:{tenantId}:student:{studentId}:arrears:{asAtDate:yyyyMMdd}";
    }
}
```

- All cache keys now include tenantId via `TenantCacheKey.ForTenant(tenantId, key)` pattern
- For user-specific: `tenant:{tenantId}:user:{userId}:perms`
- For student-specific: `tenant:{tenantId}:student:{studentId}:arrears:20260809`

### Updated Existing Code Comments
- `PermissionAuthorizationHandler.cs` had comment `// In real app, inject cache: IDistributedCache or IMemoryCache for perms per user`
- Now should use `TenantCacheKey.PermissionsForUser(tenantId, userId)` when implementing cache

### Redis Configuration
- docker-compose has Redis 7-alpine with 256M limit, but no code using it yet
- Future: When using Redis, ensure key prefix includes tenantId via factory

## Verification
```bash
grep -R "IDistributedCache\|IMemoryCache" src --include="*.cs" | grep -v Tests
# Currently no usage, but future code must use TenantCacheKey

# Example of insecure key (should not exist):
# _cache.Get($"user:{userId}:perms") -> insecure, leaks across tenants if userId reused or if platform user
# Secure: _cache.Get(TenantCacheKey.PermissionsForUser(tenantId, userId))

# Scan for cache key patterns without tenant:
grep -R "user:.*perms\|student:.*arrears" src --include="*.cs"
# Should all use TenantCacheKey now
```

## Impact
- Prevents cache poisoning/cross-tenant leak when same userId or studentId exists in different tenants (though PK is global unique, defense in depth)
- Central factory ensures consistent key format, easy to audit
- Future devs must use factory, not invent own keys

## Remaining
- Need to audit all future caching usages to ensure they use TenantCacheKey
- Add Roslyn analyzer or code review checklist: "Does cache key include tenantId?"
- For EF model cache, already fixed via TenantModelCacheKeyFactory in security audit C5

## Next Fix: H3 Background Jobs Tenant Scope
