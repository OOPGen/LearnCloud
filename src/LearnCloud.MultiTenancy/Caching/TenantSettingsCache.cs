using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LearnCloud.MultiTenancy.Caching;

// PERFORMANCE FIX H5: Caching TenantSettings to avoid DB query per request

public interface ITenantSettingsCache
{
    Task<TenantSettings?> GetAsync(long tenantId, CancellationToken ct = default);
    void Remove(long tenantId);
}

public class TenantSettingsCache : ITenantSettingsCache
{
    private readonly IMemoryCache _cache;
    private readonly LearnCloudDbContext _db;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public TenantSettingsCache(IMemoryCache cache, LearnCloudDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public async Task<TenantSettings?> GetAsync(long tenantId, CancellationToken ct = default)
    {
        var key = TenantCacheKey.ForTenant(tenantId, "settings");
        if (_cache.TryGetValue<TenantSettings>(key, out var cached))
            return cached;

        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (settings != null)
        {
            _cache.Set(key, settings, _ttl);
        }
        return settings;
    }

    public void Remove(long tenantId)
    {
        var key = TenantCacheKey.ForTenant(tenantId, "settings");
        _cache.Remove(key);
    }
}
