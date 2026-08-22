using LearnCloud.Auth.DTOs;
using LearnCloud.Auth.Entities;

namespace LearnCloud.Auth.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user, IList<string> roleCodes, IList<string> permissions);
    (string RawToken, string HashedToken) GenerateRefreshToken();
    string HashToken(string rawToken); // SHA256
    string GenerateOpaqueToken(int bytes = 64); // base64url
    string HashForStorage(string rawToken) => HashToken(rawToken);
}

public interface IAuthService
{
    Task<RegisterTenantResponse> RegisterTenantAsync(RegisterTenantRequest req, string ip, CancellationToken ct = default);
    Task<TokenResponse> LoginAsync(LoginRequest req, string ip, CancellationToken ct = default);
    Task<TokenResponse> RefreshAsync(RefreshRequest req, string ip, CancellationToken ct = default);
    Task<EmailVerificationResponse> VerifyEmailAsync(VerifyEmailRequest req, CancellationToken ct = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest req, string ip, CancellationToken ct = default);
    Task<EmailVerificationResponse> ResetPasswordAsync(ResetPasswordRequest req, string ip, CancellationToken ct = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest req, CancellationToken ct = default);
    Task<List<UserSessionDto>> GetActiveSessionsAsync(long userId, CancellationToken ct = default);
    Task RevokeSessionAsync(long userId, string familyId, string reason, CancellationToken ct = default);
    Task EnforceMaxSessionsAsync(long userId, int maxFamilies = 5, CancellationToken ct = default);
    Task RevokeAllRefreshTokensAsync(long userId, string reason, CancellationToken ct = default);
    Task<UserInfoDto> GetUserInfoAsync(long userId, long? tenantId, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string email, string displayName, string rawToken, long? tenantId);
    Task SendPasswordResetAsync(string email, string displayName, string rawToken, long? tenantId);
    Task SendWelcomeAsync(string email, string displayName, long? tenantId);
}
