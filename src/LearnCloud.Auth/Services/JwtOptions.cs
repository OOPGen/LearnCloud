namespace LearnCloud.Auth.Services;

public class JwtOptions
{
    public string Secret { get; set; } = null!; // min 32 chars HS256
    public string Issuer { get; set; } = "LearnCloud";
    public string Audience { get; set; } = "LearnCloud";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 14;
}

public class PasswordPolicyOptions
{
    public int RequiredLength { get; set; } = 8;
    public bool RequireUpper { get; set; } = true;
    public bool RequireLower { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
    public int MaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int EmailVerificationTokenHours { get; set; } = 24;
    public int PasswordResetTokenHours { get; set; } = 2;
}

public class AuthRateLimitOptions
{
    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
    public int RegistrationPermitLimit { get; set; } = 3;
    public int RegistrationWindowSeconds { get; set; } = 3600;
    public int PasswordResetPermitLimit { get; set; } = 3;
    public int PasswordResetWindowSeconds { get; set; } = 3600;
}
