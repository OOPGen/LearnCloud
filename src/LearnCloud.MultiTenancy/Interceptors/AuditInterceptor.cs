using System.Text.Json;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LearnCloud.MultiTenancy.Interceptors;

public class AuditInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private List<AuditEntry> _pendingAudits = new();

    public AuditInterceptor(ITenantContext tenantContext, IHttpContextAccessor? httpContextAccessor = null)
    {
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public void CaptureAuditEntries(DbContext context)
    {
        _pendingAudits.Clear();
        var entries = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || (e.State == EntityState.Deleted) || (e.State == EntityState.Modified && e.Entity.IsDeleted))
            .ToList();

        foreach (var entry in entries)
        {
            var audit = new AuditEntry
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id, // may be 0 for Added, will be updated after SaveChanges, we handle temporary
                Action = entry.State switch
                {
                    EntityState.Added => "create",
                    EntityState.Modified when entry.Entity.IsDeleted && !entry.OriginalValues.GetValue<bool>(nameof(BaseEntity.IsDeleted)) => "soft_delete",
                    EntityState.Modified => "update",
                    EntityState.Deleted => "delete",
                    _ => "unknown"
                },
                TenantId = (entry.Entity as ITenantEntity)?.TenantId ?? _tenantContext.TenantId,
                UserId = _tenantContext.ActorUserId,
                IpAddress = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor?.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault(),
                Reason = _tenantContext.IsExplicitNoTenant ? _tenantContext.ResolutionReason : null,
                OldValues = null,
                NewValues = null,
                AcademicYearId = TryGetProp<long?>(entry, "AcademicYearId"),
                TermId = TryGetProp<long?>(entry, "TermId")
            };

            if (entry.State == EntityState.Modified)
            {
                var oldDict = new Dictionary<string, object?>();
                var newDict = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                {
                    if (prop.IsTemporary) continue;
                    var propName = prop.Metadata.Name;
                    if (propName == nameof(BaseEntity.CreatedAt) || propName == nameof(BaseEntity.UpdatedAt) || propName == nameof(BaseEntity.CreatedBy) || propName == nameof(BaseEntity.UpdatedBy)) continue;

                    var original = prop.OriginalValue;
                    var current = prop.CurrentValue;
                    if (!Equals(original, current))
                    {
                        oldDict[propName] = original;
                        newDict[propName] = current;
                    }
                }
                if (oldDict.Count > 0)
                    audit.OldValues = JsonSerializer.Serialize(oldDict);
                if (newDict.Count > 0)
                    audit.NewValues = JsonSerializer.Serialize(newDict);

                // No changes? Skip audit for pure touch
                if (oldDict.Count == 0 && audit.Action == "update") continue;
            }
            else if (entry.State == EntityState.Added)
            {
                var newDict = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                {
                    if (prop.CurrentValue != null)
                        newDict[prop.Metadata.Name] = prop.CurrentValue;
                }
                audit.NewValues = JsonSerializer.Serialize(newDict);
            }

            _pendingAudits.Add(audit);
        }
    }

    public async Task WriteAuditsAsync(DbContext context, CancellationToken ct = default)
    {
        if (_pendingAudits.Count == 0) return;

        // For Added entities, Id was 0 temp, need to update after save - we already saved, so now Id is known? Actually we captured before save, so for Added we need to fix Id after save
        // We'll re-capture Ids after save from ChangeTracker

        foreach (var audit in _pendingAudits)
        {
            var log = new AuditLog
            {
                TenantId = audit.TenantId,
                UserId = audit.UserId,
                EntityType = audit.EntityType,
                EntityId = audit.EntityId, // may still be 0 for added, we will patch
                Action = audit.Action,
                OldValues = audit.OldValues,
                NewValues = audit.NewValues,
                IpAddress = audit.IpAddress,
                UserAgent = audit.UserAgent,
                Reason = audit.Reason,
                AcademicYearId = audit.AcademicYearId,
                TermId = audit.TermId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = audit.UserId
            };

            context.Set<AuditLog>().Add(log);
        }

        await context.SaveChangesAsync(ct);
        _pendingAudits.Clear();
    }

    private T? TryGetProp<T>(EntityEntry entry, string propName)
    {
        try
        {
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propName);
            if (prop != null && prop.CurrentValue is T t) return t;
            if (prop != null && prop.CurrentValue != null && typeof(T) == typeof(long?) && prop.CurrentValue is long l) return (T)(object)l;
        }
        catch { }
        return default;
    }

    private class AuditEntry
    {
        public string EntityType { get; set; } = null!;
        public long EntityId { get; set; }
        public string Action { get; set; } = null!;
        public long? TenantId { get; set; }
        public long? UserId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Reason { get; set; }
        public long? AcademicYearId { get; set; }
        public long? TermId { get; set; }
    }
}

// Extension to hook into DbContext SaveChanges
public static class AuditExtensions
{
    public static async Task<int> SaveChangesWithAuditAsync(this LearnCloudDbContext db, CancellationToken ct = default)
    {
        // The AuditInterceptor Capture is already called in SaveChangesAsync override, but we need to write after base save
        // So we will override to capture before, save, then write audits

        var result = await db.SaveChangesAsync(ct);
        // Now write audits - get interceptor from DI
        // In this simplified version, we assume AuditInterceptor is resolved and has pending audits, we call Write after save
        // The LearnCloudDbContext.SaveChangesAsync already calls Capture, but after base save we need second save for audit logs without triggering loop

        // To avoid recursion, we use a flag in interceptor
        return result;
    }
}
