using LearnCloud.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.Auth.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.Email).HasColumnType("VARCHAR(255)").IsRequired();
        b.Property(x => x.Phone).HasColumnType("VARCHAR(50)");
        b.Property(x => x.DisplayName).HasColumnType("VARCHAR(255)").IsRequired();
        b.Property(x => x.PasswordHash).HasColumnType("VARCHAR(500)").IsRequired();
        b.Property(x => x.EmailVerified).HasDefaultValue(false);
        b.Property(x => x.TokenVersion).HasDefaultValue(1);
        b.Property(x => x.SecurityStamp).HasColumnType("VARCHAR(100)").IsRequired();
        b.Property(x => x.Status).HasColumnType("VARCHAR(20)").HasDefaultValue("active");
        b.Property(x => x.FailedLoginCount).HasDefaultValue(0);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        b.Property(x => x.IsDeleted).HasDefaultValue(false);

        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique().HasDatabaseName("uq_users_tenant_email");
        b.HasIndex(x => x.Email).HasDatabaseName("idx_users_email_global");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("idx_users_tenant_status");
        b.HasIndex(x => x.SecurityStamp).HasDatabaseName("idx_users_security_stamp");

        b.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.UserId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.TokenHash).HasColumnType("VARCHAR(128)").IsRequired(); // SHA256 hex 64 chars
        b.Property(x => x.FamilyId).HasColumnType("VARCHAR(100)").IsRequired();
        b.Property(x => x.ParentTokenId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.RevokedReason).HasColumnType("VARCHAR(100)");
        b.Property(x => x.CreatedByIp).HasColumnType("VARCHAR(45)").IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.IsDeleted).HasDefaultValue(false);

        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_refresh_token_hash");
        b.HasIndex(x => new { x.TenantId, x.UserId, x.FamilyId }).HasDatabaseName("idx_refresh_tenant_user_family");
        b.HasIndex(x => new { x.TenantId, x.UserId, x.ExpiresAt }).HasDatabaseName("idx_refresh_tenant_user_exp");
        b.HasIndex(x => x.FamilyId).HasDatabaseName("idx_refresh_family");

        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ParentToken).WithMany().HasForeignKey(x => x.ParentTokenId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> b)
    {
        b.ToTable("user_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.TenantId).HasColumnType("BIGINT").IsRequired(false);
        b.Property(x => x.UserId).HasColumnType("BIGINT").IsRequired();
        b.Property(x => x.TokenType).HasConversion<int>().IsRequired();
        b.Property(x => x.TokenHash).HasColumnType("VARCHAR(128)").IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        b.HasIndex(x => x.TokenHash).HasDatabaseName("idx_usertoken_hash");
        b.HasIndex(x => new { x.UserId, x.TokenType, x.ExpiresAt }).HasDatabaseName("idx_usertoken_user_type_exp");
        b.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_usertoken_expires");

        b.HasOne(x => x.User).WithMany(u => u.UserTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("BIGINT").ValueGeneratedOnAdd();
        b.Property(x => x.Name).HasColumnType("VARCHAR(255)").IsRequired();
        b.Property(x => x.Slug).HasColumnType("VARCHAR(100)").IsRequired();
        b.Property(x => x.Status).HasColumnType("VARCHAR(20)").HasDefaultValue("trial");
        b.Property(x => x.City).HasColumnType("VARCHAR(100)").HasDefaultValue("Bulawayo");
        b.Property(x => x.Country).HasColumnType("CHAR(2)").HasDefaultValue("ZW");
        b.Property(x => x.ContactEmail).HasColumnType("VARCHAR(255)").IsRequired();
        b.Property(x => x.PrimaryColor).HasColumnType("CHAR(7)").HasDefaultValue("#0F153A");
        b.Property(x => x.LearnerCountBand).HasColumnType("VARCHAR(20)").IsRequired();
        b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_tenants_slug");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.IsDeleted).HasDefaultValue(false);
    }
}
