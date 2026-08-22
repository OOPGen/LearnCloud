using LearnCloud.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.Auth.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.Code).HasColumnType("VARCHAR(50)").IsRequired();
        b.Property(x => x.Name).HasColumnType("VARCHAR(100)").IsRequired();
        b.Property(x => x.IsSystem).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.IsDeleted).HasDefaultValue(false);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("uq_roles_tenant_code");
        // SECURITY DB FIX C1: Removed duplicate idx_roles_tenant_code same columns - keeps only unique
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.Code).HasColumnType("VARCHAR(100)").IsRequired();
        b.Property(x => x.Name).HasColumnType("VARCHAR(150)").IsRequired();
        b.Property(x => x.Module).HasColumnType("VARCHAR(50)").IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_permissions_code");
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.RoleId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.PermissionId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.TenantId, x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("uq_rp_tenant_role_perm");
        b.HasIndex(x => new { x.TenantId, x.RoleId }).HasDatabaseName("idx_rp_tenant_role");
        b.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.UserId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.RoleId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.AcademicYearId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId, x.AcademicYearId }).IsUnique().HasDatabaseName("uq_user_roles");
        b.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_user_roles_tenant_user");
        b.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("subscription_plans");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.Code).HasColumnType("VARCHAR(50)").IsRequired();
        b.Property(x => x.Name).HasColumnType("VARCHAR(100)").IsRequired();
        b.Property(x => x.MaxLearners).IsRequired();
        b.Property(x => x.PriceMonthly).HasColumnType("DECIMAL(18,2)").HasPrecision(18,2).IsRequired(); // DB FIX H8
        b.Property(x => x.PriceAnnual).HasColumnType("DECIMAL(18,2)").HasPrecision(18,2).IsRequired();
        b.Property(x => x.Currency).HasColumnType("CHAR(3)").HasDefaultValue("USD");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_plans_code");
    }
}

public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> b)
    {
        b.ToTable("tenant_subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.PlanId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.BillingCycle).HasColumnType("VARCHAR(20)").HasDefaultValue("monthly");
        b.Property(x => x.Status).HasColumnType("VARCHAR(20)").HasDefaultValue("trialing");
        b.Property(x => x.Currency).HasColumnType("CHAR(3)").HasDefaultValue("USD");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("idx_tenant_subs_tenant_status");
        b.HasOne(x => x.Tenant).WithMany(t => t.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
