using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.MultiTenancy.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.Property(x => x.Status).HasDefaultValue("trial");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // A deleted school's slug may be taken again (the original schema intended this).
        b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_tenants_slug").HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasMany(x => x.Domains).WithOne().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Settings).WithOne().HasForeignKey<TenantSettings>(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> b)
    {
        b.HasIndex(d => d.Domain).IsUnique().HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasIndex(d => new { d.TenantId, d.Domain }).IsUnique().HasFilter(SoftDelete.ActiveRowsFilter);
    }
}

public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> b)
    {
        b.HasIndex(s => s.TenantId).IsUnique().HasFilter(SoftDelete.ActiveRowsFilter);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId });
    }
}
