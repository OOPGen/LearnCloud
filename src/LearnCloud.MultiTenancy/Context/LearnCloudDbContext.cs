using System.Linq.Expressions;
using System.Reflection;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.MultiTenancy.Interceptors;
using LearnCloud.MultiTenancy.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LearnCloud.MultiTenancy.Context;

// The one database context for the whole platform. Every module's entities are part
// of this model (see LearnCloudModel), so a service in any module can query any
// entity through Set<T>().
//
// Tenant isolation: every ITenantEntity gets a global query filter that reads
// CurrentTenantId from *this* context instance. EF Core re-evaluates members of the
// current context on every query, so one cached model serves all tenants safely.
// The previous design compiled a separate model per tenant, which does not scale
// and was not needed for correctness.
public class LearnCloudDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly AuditInterceptor _auditInterceptor;

    public LearnCloudDbContext(DbContextOptions<LearnCloudDbContext> options, ITenantContext tenantContext, AuditInterceptor auditInterceptor) : base(options)
    {
        _tenantContext = tenantContext;
        _auditInterceptor = auditInterceptor;
    }

    // Platform-level tables owned by this module. Everything else: Set<T>().
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Read by the query filters on every query.
    private long? CurrentTenantId => _tenantContext.TenantId;

    private bool IsExplicitNoTenant
    {
        get
        {
            if (!_tenantContext.IsExplicitNoTenant) return false;
            if (!PrivilegedRoles.CanUseNoTenantScope(_tenantContext.ActorRole))
                throw new UnauthorizedAccessException(
                    $"SECURITY: tenant filter bypass requires one of {string.Join(",", PrivilegedRoles.All)}, got {_tenantContext.ActorRole}. Actor {_tenantContext.ActorUserId} reason {_tenantContext.ResolutionReason}");
            return true;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in LearnCloudModel.Assemblies)
        {
            foreach (var type in LearnCloudModel.EntityTypes(assembly))
                modelBuilder.Entity(type);
            modelBuilder.ApplyConfigurationsFromAssembly(assembly, t => t.Namespace?.Split('.').Contains("Tests") != true);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.BaseType is not null || entityType.IsOwned()) continue;
            var clrType = entityType.ClrType;

            // Plural table names unless a configuration chose one explicitly (e.g. "users").
            // The snake_case convention sets a name on every entity as it is added, so the
            // check must look at how the name was configured, not whether one exists.
            if (((IConventionEntityType)entityType).GetTableNameConfigurationSource() != ConfigurationSource.Explicit)
                entityType.SetTableName(LearnCloudModel.PluralTableName(clrType.Name));

            if (typeof(ITenantEntity).IsAssignableFrom(clrType) && typeof(BaseEntity).IsAssignableFrom(clrType))
                entityType.SetQueryFilter(BuildFilter(nameof(TenantFilter), clrType));
            else if (typeof(BaseEntity).IsAssignableFrom(clrType) && clrType != typeof(Tenant) && clrType != typeof(AuditLog))
                entityType.SetQueryFilter(BuildFilter(nameof(SoftDeleteFilter), clrType));
        }
    }

    private LambdaExpression BuildFilter(string method, Type clrType) =>
        (LambdaExpression)typeof(LearnCloudDbContext)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(clrType)
            .Invoke(this, null)!;

    private LambdaExpression TenantFilter<TEntity>() where TEntity : BaseEntity, ITenantEntity
    {
        Expression<Func<TEntity, bool>> filter = e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId);
        return filter;
    }

    private LambdaExpression SoftDeleteFilter<TEntity>() where TEntity : BaseEntity
    {
        Expression<Func<TEntity, bool>> filter = e => !e.IsDeleted;
        return filter;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        // The synchronous path used to bypass tenant stamping and audit entirely.
        ApplyTenantRulesAndAuditStamps();
        _auditInterceptor.CaptureAuditEntries(this);
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        _auditInterceptor.WriteAuditsAsync(this).GetAwaiter().GetResult();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantRulesAndAuditStamps();
        _auditInterceptor.CaptureAuditEntries(this);
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await _auditInterceptor.WriteAuditsAsync(this, cancellationToken);
        return result;
    }

    private void ApplyTenantRulesAndAuditStamps()
    {
        var now = DateTime.UtcNow;
        var actor = _tenantContext.ActorUserId;

        // Deleted must be included: before, only Added/Modified entries were examined,
        // so the soft-delete branch never ran and every Remove() was a hard DELETE.
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedBy = actor;
            }

            if (entry.Entity is ITenantEntity tenantEntity)
                EnforceTenantOwnership(entry, tenantEntity);

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy ??= actor;
                entry.Entity.UpdatedAt = now;
            }
            else
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = actor;
            }
        }
    }

    private void EnforceTenantOwnership(EntityEntry<BaseEntity> entry, ITenantEntity tenantEntity)
    {
        var name = entry.Entity.GetType().Name;
        var contextTenant = _tenantContext.TenantId;
        var bypass = IsExplicitNoTenant;

        if (entry.State == EntityState.Added)
        {
            if (tenantEntity.TenantId == 0)
            {
                if (contextTenant.HasValue) { tenantEntity.TenantId = contextTenant.Value; return; }
                throw new InvalidOperationException(bypass
                    ? $"Explicit no-tenant save requires TenantId to be set on {name}. Actor {_tenantContext.ActorUserId} role {_tenantContext.ActorRole}"
                    : $"Cannot save {name} without TenantId: no tenant context resolved.");
            }
            if (contextTenant.HasValue && tenantEntity.TenantId != contextTenant.Value && !bypass)
                throw new InvalidOperationException($"TenantId mismatch: {name} has TenantId {tenantEntity.TenantId} but the request tenant is {contextTenant}. Possible cross-tenant write.");
            return;
        }

        var originalTenantId = entry.OriginalValues.GetValue<long>(nameof(ITenantEntity.TenantId));
        if (originalTenantId != tenantEntity.TenantId)
            throw new InvalidOperationException($"Changing TenantId is forbidden for {name}. Original {originalTenantId} -> New {tenantEntity.TenantId}");
        if (contextTenant.HasValue && tenantEntity.TenantId != contextTenant.Value && !bypass)
            throw new InvalidOperationException($"TenantId mismatch on update: {name} TenantId {tenantEntity.TenantId} vs request tenant {contextTenant}");
    }
}
