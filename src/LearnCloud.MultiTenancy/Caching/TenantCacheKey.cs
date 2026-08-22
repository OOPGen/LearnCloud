namespace LearnCloud.MultiTenancy.Caching;

// SECURITY FIX C4: Caching without TenantId in key could leak across tenants
// All cache keys must include tenantId

public static class TenantCacheKey
{
    public static string ForTenant(long? tenantId, string key)
    {
        // If tenantId null (platform), use platform prefix
        var tenantPrefix = tenantId.HasValue ? $"tenant:{tenantId}" : "platform";
        return $"{tenantPrefix}:{key}";
    }

    public static string ForUser(long? tenantId, long userId, string key)
    {
        return $"{ForTenant(tenantId, key)}:user:{userId}";
    }

    public static string ForStudent(long tenantId, long studentId, string key)
    {
        return $"tenant:{tenantId}:student:{studentId}:{key}";
    }

    // Example: permissions cache
    public static string PermissionsForUser(long? tenantId, long userId)
    {
        return ForUser(tenantId, userId, "perms");
    }

    // Fee calculation cache
    public static string FeeArrears(long tenantId, long studentId, DateTime asAtDate)
    {
        return $"tenant:{tenantId}:student:{studentId}:arrears:{asAtDate:yyyyMMdd}";
    }
}
