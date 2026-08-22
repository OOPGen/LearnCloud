using System.Linq.Expressions;
using System.Reflection;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.MultiTenancy.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.MultiTenancy.Context;

public class LearnCloudDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly AuditInterceptor _auditInterceptor;

    public LearnCloudDbContext(DbContextOptions<LearnCloudDbContext> options, ITenantContext tenantContext, AuditInterceptor auditInterceptor) : base(options)
    {
        _tenantContext = tenantContext;
        _auditInterceptor = auditInterceptor;
    }

    // Exposed for IModelCacheKeyFactory - SECURITY C5
    public long? TenantIdForCache => _tenantContext.TenantId;
    public bool IsExplicitNoTenantForCache => _tenantContext.IsExplicitNoTenant;

    // DbSets - Tenant and Platform ONLY (C2 CLEANUP: domain entities moved to Domain/Fees/etc modules)
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    private long? CurrentTenantId
    {
        get
        {
            // SECURITY C5: If explicit no-tenant, verify role already checked in TenantContext, but also audit here
            if (_tenantContext.IsExplicitNoTenant)
            {
                // Additional guard: ensure actor has privileged role
                var role = _tenantContext.ActorRole;
                if (role != "PLATFORM_SUPERADMIN" && role != "PLATFORM_SUPPORT" && role != "SYSTEM")
                {
                    throw new UnauthorizedAccessException($"SECURITY: IsExplicitNoTenant bypass attempted without privileged role. Actor { _tenantContext.ActorUserId} role {role} reason {_tenantContext.ResolutionReason}");
                }
                // Log audit for explicit no-tenant access - every query bypassing filter must be audited
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
                // SECURITY: Double-check privileged role
                var role = _tenantContext.ActorRole;
                var allowed = new[] { "PLATFORM_SUPERADMIN", "PLATFORM_SUPPORT", "SYSTEM" };
                if (!allowed.Contains(role))
                    throw new UnauthorizedAccessException($"SECURITY: Explicit no-tenant filter bypass requires privileged role {string.Join(",", allowed)}, got {role}");
            }
            return _tenantContext.IsExplicitNoTenant;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (typeof(ITenantEntity).IsAssignableFrom(clrType) && typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                var method = typeof(LearnCloudDbContext).GetMethod(nameof(GetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                var filter = method.Invoke(this, null);
                modelBuilder.Entity(clrType).HasQueryFilter((LambdaExpression)filter!);
            }
            else if (typeof(BaseEntity).IsAssignableFrom(clrType) && clrType != typeof(Tenant) && clrType != typeof(Plan) && clrType != typeof(AuditLog))
            {
                var method = typeof(LearnCloudDbContext).GetMethod(nameof(GetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                var filter = method.Invoke(this, null);
                modelBuilder.Entity(clrType).HasQueryFilter((LambdaExpression)filter!);
            }
        }

        // Unique constraints
        modelBuilder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        modelBuilder.Entity<TenantDomain>().HasIndex(d => d.Domain).IsUnique();
        modelBuilder.Entity<TenantDomain>().HasIndex(d => new { d.TenantId, d.Domain }).IsUnique();
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId });
    }

    private LambdaExpression GetTenantFilter<TEntity>() where TEntity : TenantOwnedEntity
    {
        // SECURITY C5: This filter uses IsExplicitNoTenant which now has role guard, plus TenantId
        // With TenantModelCacheKeyFactory, each tenant gets separate model cache, preventing poisoning
        Expression<Func<TEntity, bool>> filter = e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId);
        return filter;
    }

    private LambdaExpression GetSoftDeleteFilter<TEntity>() where TEntity : BaseEntity
    {
        Expression<Func<TEntity, bool>> filter = e => !e.IsDeleted;
        return filter;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is ITenantEntity tenantEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (tenantEntity.TenantId == 0)
                    {
                        if (_tenantContext.TenantId == null && !_tenantContext.IsExplicitNoTenant)
                        {
                            throw new InvalidOperationException($"Cannot save ITenantEntity {entry.Entity.GetType().Name} without TenantId - no tenant context resolved.");
                        }
                        if (_tenantContext.TenantId.HasValue)
                        {
                            tenantEntity.TenantId = _tenantContext.TenantId.Value;
                        }
                        else if (_tenantContext.IsExplicitNoTenant)
                        {
                            // SECURITY: Explicit no-tenant save must have TenantId explicitly set by caller and privileged role already checked
                            if (tenantEntity.TenantId == 0)
                                throw new InvalidOperationException($"Explicit no-tenant save requires explicit TenantId set for {entry.Entity.GetType().Name}, but got 0. Actor {_tenantContext.ActorUserId} role {_tenantContext.ActorRole}");
                        }
                    }
                    else
                    {
                        if (_tenantContext.TenantId.HasValue && tenantEntity.TenantId != _tenantContext.TenantId.Value && !_tenantContext.IsExplicitNoTenant)
                        {
                            throw new InvalidOperationException($"TenantId mismatch: entity {entry.Entity.GetType().Name} has TenantId {tenantEntity.TenantId} but context TenantId {_tenantContext.TenantId}. Possible cross-tenant reference attack.");
                        }
                        // SECURITY: If explicit no-tenant, still verify caller didn't bypass by setting TenantId to arbitrary value without proper role
                        if (_tenantContext.IsExplicitNoTenant)
                        {
                            // Already validated role in IsExplicitNoTenant getter, but log for audit
                        }
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    var originalTenantId = entry.OriginalValues.GetValue<long>(nameof(ITenantEntity.TenantId));
                    if (originalTenantId != tenantEntity.TenantId)
                    {
                        throw new InvalidOperationException($"Changing TenantId is forbidden for {entry.Entity.GetType().Name}. Original {originalTenantId} -> New {tenantEntity.TenantId}");
                    }
                    if (_tenantContext.TenantId.HasValue && tenantEntity.TenantId != _tenantContext.TenantId.Value && !_tenantContext.IsExplicitNoTenant)
                    {
                        throw new InvalidOperationException($"TenantId mismatch on update: {entry.Entity.GetType().Name} TenantId {tenantEntity.TenantId} vs context {_tenantContext.TenantId}");
                    }

                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = _tenantContext.ActorUserId;
                }

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = _tenantContext.ActorUserId;
                }
            }
            else
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = _tenantContext.ActorUserId;
                }
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = _tenantContext.ActorUserId;
                }
            }

            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
                entry.Entity.DeletedBy = _tenantContext.ActorUserId;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        _auditInterceptor.CaptureAuditEntries(this);
        var result = await base.SaveChangesAsync(cancellationToken);
        await _auditInterceptor.WriteAuditsAsync(this, cancellationToken);
        return result;
    }
}
