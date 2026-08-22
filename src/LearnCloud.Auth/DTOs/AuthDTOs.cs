namespace LearnCloud.Auth.DTOs;

// Self-registration: creates tenant + first admin + trial in one transaction
public record RegisterTenantRequest(
    string SchoolName,          // e.g. Petra High
    string Slug,                // petra -> petra.learncloud.co.zw
    string City,                // Bulawayo
    string ContactEmail,
    string ContactPhone,
    string LearnerCountBand,    // 150-300, 301-800, 801-2000
    string AdminFullName,
    string AdminEmail,
    string AdminPhone,
    string Password,
    string ConfirmPassword
);

public record RegisterTenantResponse(
    long TenantId,
    string Slug,
    long AdminUserId,
    string TrialEndsAt,
    bool EmailVerificationRequired
);

public record LoginRequest(
    string Email,
    string Password,
    string? TenantSlug, // optional: if null, infer from email domain or platform login
    string? Device // e.g. "Chrome Windows"
);

public record TokenResponse(
    string AccessToken, // JWT 15min
    string RefreshToken, // opaque 64 bytes base64url, 14d
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    long UserId,
    long? TenantId,
    string DisplayName
);

public record RefreshRequest(
    string RefreshToken,
    string? Device
);

public record VerifyEmailRequest(
    string Email,
    string Token // raw token from email link
);

public record ForgotPasswordRequest(
    string Email, // we never reveal if exists
    string? TenantSlug // to scope lookup
);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);

public record EmailVerificationResponse(bool Verified, string Message);
public record ForgotPasswordResponse(string Message); // always same message

public record UserInfoDto(
    long Id,
    long? TenantId,
    string Email,
    string DisplayName,
    bool EmailVerified,
    string Status,
    int TokenVersion,
    DateTime? LastLoginAt,
    List<string> RoleCodes,
    List<string> Permissions
);

public record UserSessionDto(string FamilyId, string Device, string Ip, DateTime CreatedAt, DateTime ExpiresAt, string CurrentFamilyId);
