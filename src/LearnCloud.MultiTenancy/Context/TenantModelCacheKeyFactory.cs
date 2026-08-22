using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LearnCloud.MultiTenancy.Context;

// SECURITY C5 FIX: IModelCacheKeyFactory with tenantId to prevent EF model cache poisoning
// Without this, once a model is cached for tenant A, tenant B could get cached model without filter, or with wrong tenant filter
// EF Core caches model per DbContext type, but our filter uses CurrentTenantId which is instance property - need to include tenant in cache key

public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is LearnCloudDbContext tenantContext)
        {
            // Include tenantId and explicit no-tenant flag in cache key
            // This ensures each tenant gets its own model cache entry with correct filter
            return (context.GetType(), tenantContext.TenantIdForCache, tenantContext.IsExplicitNoTenantForCache, designTime);
        }
        return (context.GetType(), designTime);
    }
}
