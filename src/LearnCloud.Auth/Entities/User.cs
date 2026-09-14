using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Auth.Entities;

// Auth entities share the platform base class from LearnCloud.MultiTenancy, so the
// main context's soft-delete filter and audit stamping apply to them. They are not
// ITenantEntity: login and token lookups must work before a tenant is resolved, so
// services filter by TenantId explicitly.
public class User : BaseEntity
{
    // Nullable for platform superadmin
    public long? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string DisplayName { get; set; } = null!;
    // ASP.NET Identity v3 hasher output: AQAAAAEAACcQ...
    public string PasswordHash { get; set; } = null!;

    public bool EmailVerified { get; set; } = false;
    public DateTime? EmailVerifiedAt { get; set; }

    // Security stamp for revocation (token version) - increments on password change
    public int TokenVersion { get; set; } = 1;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    // Lockout
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public string Status { get; set; } = "invited"; // invited, active, disabled
    public bool MustChangePassword { get; set; } = false;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();

    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
}

public enum UserTokenType
{
    EmailVerification = 1,
    PasswordReset = 2
}

public class UserToken : BaseEntity
{
    // Nullable: platform users have no tenant, and tokens are looked up by hash
    // before any tenant is resolved, so this must not carry the tenant query filter.
    public long? TenantId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public UserTokenType TokenType { get; set; }
    public string TokenHash { get; set; } = null!; // SHA256 hashed
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool IsUsed => UsedAt.HasValue;
}

public class RefreshToken : BaseEntity
{
    // Inherits BaseEntity but we also need TenantId - platform refresh should have tenantId nullable; we store in separate column for uniformity
    public long? TenantId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!; // SHA256 of raw token
    public string FamilyId { get; set; } = Guid.NewGuid().ToString(); // token family for reuse detection
    public long? ParentTokenId { get; set; }
    public RefreshToken? ParentToken { get; set; }
    public long? ReplacedByTokenId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; } // reuse_detected, logout, password_changed
    public string CreatedByIp { get; set; } = null!;
    public string? Device { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;
}
