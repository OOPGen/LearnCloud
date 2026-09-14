using LearnCloud.Auth.Entities;
using LearnCloud.MultiTenancy.Entities;
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
        // Nulls not distinct: system roles have no tenant, and PostgreSQL would otherwise
        // allow any number of system roles with the same code.
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("uq_roles_tenant_code")
            .HasFilter(SoftDelete.ActiveRowsFilter).AreNullsDistinct(false);
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
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_permissions_code").HasFilter(SoftDelete.ActiveRowsFilter);
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
        b.HasIndex(x => new { x.TenantId, x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("uq_rp_tenant_role_perm")
            .HasFilter(SoftDelete.ActiveRowsFilter).AreNullsDistinct(false);
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
        b.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId, x.AcademicYearId }).IsUnique().HasDatabaseName("uq_user_roles")
            .HasFilter(SoftDelete.ActiveRowsFilter).AreNullsDistinct(false);
        b.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_user_roles_tenant_user");
        b.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

// Subscription plans and tenant subscriptions are owned by LearnCloud.PlatformBilling
// (Plan, Subscription with its state machine). Auth's parallel copies were removed.
