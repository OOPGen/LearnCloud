using LearnCloud.Auth.Authorization;
using LearnCloud.Auth.DTOs;
using LearnCloud.Auth.Entities;
using LearnCloud.Auth.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LearnCloud.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService auth, ILogger<AuthController> logger)
    {
        _auth = auth; _logger = logger;
    }

    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "unknown";

    // POST /api/auth/register-tenant - Rate limited registration 3 per hour
    [HttpPost("register-tenant")]
    [EnableRateLimiting("registration")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest req, CancellationToken ct)
    {
        var result = await _auth.RegisterTenantAsync(req, GetIp(), ct);
        return Ok(result);
    }

    // POST /api/auth/login - rate limited 5 per minute - SECURITY C2+C4 FIX
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _auth.LoginAsync(req, GetIp(), ct);
            // SECURITY C2 FIX: Set refresh token as __Host- HttpOnly Secure SameSite=Strict cookie
            // - No Domain attribute (required for __Host- prefix)
            // - Path=/ for Strict, but we use Path=/api/auth for scope? For __Host- must be Path=/
            // - Secure, HttpOnly, SameSite=Strict prevents CSRF (C4)
            // - Also return accessToken in body for memory storage, refreshToken in body ONLY for mobile (SecureStore)
            // Web clients MUST ignore refreshToken in body and use cookie only
            SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
            
            // SECURITY: For web, we should NOT return refresh token in body to prevent XSS theft
            // But mobile needs it in SecureStore. We differentiate via X-Client-Type header or User-Agent
            var clientType = Request.Headers["X-Client-Type"].FirstOrDefault() ?? "";
            var isMobile = clientType.Equals("mobile", StringComparison.OrdinalIgnoreCase) || req.Device?.ToLower().Contains("mobile") == true;
            
            if (isMobile)
            {
                // Mobile gets refresh token in body for SecureStore
                return Ok(result);
            }
            else
            {
                // Web: return only accessToken, not refreshToken (cookie contains it)
                // This prevents XSS from stealing refresh token even if accessToken in memory is stolen, refresh remains HttpOnly
                var webResult = new TokenResponseWithoutRefresh(result.AccessToken, result.AccessTokenExpiresAt, result.UserId, result.TenantId, result.DisplayName);
                return Ok(webResult);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? req, CancellationToken ct)
    {
        // SECURITY C4 FIX: CSRF protection for cookie-based refresh
        // If token from cookie, require custom header X-Requested-With or X-CSRF-Token
        // This prevents CSRF via form POST from attacker.com (SameSite Strict already mitigates, but defense in depth)
        
        var tokenFromCookie = Request.Cookies["__Host-refresh_token"] ?? Request.Cookies["refresh_token"]; // support legacy during migration
        var tokenFromBody = req?.RefreshToken;
        var token = !string.IsNullOrEmpty(tokenFromBody) ? tokenFromBody : tokenFromCookie;
        if (string.IsNullOrEmpty(token)) return Unauthorized(new { message = "Missing refresh token" });

        // If using cookie, require CSRF header
        bool isCookieAuth = !string.IsNullOrEmpty(tokenFromCookie) && string.IsNullOrEmpty(tokenFromBody);
        if (isCookieAuth)
        {
            var hasCsrfHeader = Request.Headers.ContainsKey("X-Requested-With") || Request.Headers.ContainsKey("X-CSRF-Token");
            if (!hasCsrfHeader)
            {
                _logger.LogWarning("SECURITY: Refresh via cookie without CSRF header - possible CSRF - IP {Ip} Path {Path}", GetIp(), Request.Path);
                // For Strict SameSite, this would be blocked anyway for cross-site, but we enforce
                return BadRequest(new { message = "Missing CSRF header X-Requested-With for cookie refresh" });
            }
        }

        try
        {
            var result = await _auth.RefreshAsync(new RefreshRequest(token, req?.Device), GetIp(), ct);
            SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
            
            // Same logic as login: mobile gets refresh token, web does not
            var clientType = Request.Headers["X-Client-Type"].FirstOrDefault() ?? "";
            var isMobile = clientType.Equals("mobile", StringComparison.OrdinalIgnoreCase) || req?.Device?.ToLower().Contains("mobile") == true;
            if (isMobile)
                return Ok(result);
            else
                return Ok(new TokenResponseWithoutRefresh(result.AccessToken, result.AccessTokenExpiresAt, result.UserId, result.TenantId, result.DisplayName));
        }
        catch (UnauthorizedAccessException ex)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _auth.RevokeAllRefreshTokensAsync(userId, "logout", ct);
        ClearRefreshCookie();
        return Ok(new { message = "Logged out" });
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("verify_email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
    {
        var result = await _auth.VerifyEmailAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("password_reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var result = await _auth.ForgotPasswordAsync(req, GetIp(), ct);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("password_reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var result = await _auth.ResetPasswordAsync(req, GetIp(), ct);
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("password_reset")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            await _auth.ChangePasswordAsync(userId, req, ct);
            ClearRefreshCookie();
            return Ok(new { message = "Password changed, all sessions revoked. Please login again." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var tenantId = User.GetTenantId();
        var info = await _auth.GetUserInfoAsync(userId, tenantId, ct);
        return Ok(info);
    }

    [HttpGet("students")]
    [RequiresPermission("students.read")]
    public IActionResult ExampleStudentsRead()
    {
        var tenantId = User.GetTenantId();
        return Ok(new { message = $"You have students.read for tenant {tenantId}", user = User.Identity?.Name });
    }

    // SECURITY H8: Session management - list active sessions
    [HttpGet("sessions")]
    [Authorize]
    [EnableRateLimiting("api_general")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var sessions = await _auth.GetActiveSessionsAsync(userId, ct);
        return Ok(sessions);
    }

    [HttpDelete("sessions/{familyId}")]
    [Authorize]
    [EnableRateLimiting("api_general")]
    public async Task<IActionResult> RevokeSession(string familyId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _auth.RevokeSessionAsync(userId, familyId, "user_revoked", ct);
        // If revoking current session, also clear cookie
        ClearRefreshCookie();
        return Ok(new { message = $"Session {familyId} revoked" });
    }

    [HttpGet("sessions/devices")]
    [Authorize]
    public async Task<IActionResult> GetDeviceInfo(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var tenantId = User.GetTenantId();
        // Return current device from claims or header
        var device = Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
        var ip = GetIp();
        return Ok(new { userId, tenantId, currentDevice = device, currentIp = ip, timestamp = DateTime.UtcNow });
    }

    [HttpPost("unlock/{userId:long}")]
    [RequiresPermission("users.disable")]
    public async Task<IActionResult> UnlockUser(long userId, CancellationToken ct)
    {
        var tenantId = User.GetTenantId();
        var db = HttpContext.RequestServices.GetRequiredService<LearnCloudDbContext>();
        var target = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted, ct);
        if (target == null) return NotFound();
        target.FailedLoginCount = 0;
        target.LockoutEnd = null;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("User {TargetId} unlocked by {AdminId}", userId, User.GetUserId());
        return Ok(new { message = "Unlocked" });
    }

    // SECURITY C2+C4 FIX: __Host- prefix requires Secure, Path=/, no Domain. SameSite=Strict prevents CSRF.
    // For migration, we still support old refresh_token cookie but set new one as primary
    private void SetRefreshCookie(string token, DateTime expires)
    {
        // New secure cookie with __Host- prefix (requires Secure, Path=/, no Domain)
        Response.Cookies.Append("__Host-refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict, // SECURITY C4: Strict prevents CSRF
            Expires = expires,
            Path = "/", // __Host- requires Path=/
            // No Domain attribute for __Host-
        });
        // Also set legacy cookie for transition period (remove after 30 days)
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/api/auth",
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete("__Host-refresh_token", new CookieOptions { Path = "/", Secure = true, SameSite = SameSiteMode.Strict });
        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/auth" });
    }
}

// For web clients, we don't return refresh token in body - only access token
public record TokenResponseWithoutRefresh(string AccessToken, DateTime AccessTokenExpiresAt, long UserId, long? TenantId, string DisplayName);
