using System.Security.Cryptography;
using LearnCloud.Auth.DTOs;
using LearnCloud.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.PlatformBilling.Entities;

namespace LearnCloud.Auth.Services;

// Auth used to run on its own AuthDbContext mapping the same tables as the platform
// context, with its own copies of Tenant, SubscriptionPlan and TenantSubscription.
// It now uses LearnCloudDbContext and the canonical entities.

public class AuthService : IAuthService
{
    private readonly LearnCloudDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<JwtOptions> _jwt;
    private readonly IOptions<PasswordPolicyOptions> _pwdPol;
    private readonly ILogger<AuthService> _logger;

    public AuthService(LearnCloudDbContext db, IPasswordHasher<User> hasher, ITokenService tokenService, IEmailSender emailSender, IOptions<JwtOptions> jwt, IOptions<PasswordPolicyOptions> pwdPol, ILogger<AuthService> logger)
    {
        _db = db; _hasher = hasher; _tokenService = tokenService; _emailSender = emailSender; _jwt = jwt; _pwdPol = pwdPol; _logger = logger;
    }

    // 1. School self-registration transactional: tenant + admin user + 14d trial subscription
    public async Task<RegisterTenantResponse> RegisterTenantAsync(RegisterTenantRequest req, string ip, CancellationToken ct = default)
    {
        // Check slug unique
        var slugExists = await _db.Tenants.AnyAsync(t => t.Slug == req.Slug && !t.IsDeleted, ct);
        if (slugExists) throw new InvalidOperationException("Slug already taken");

        var emailExists = await _db.Set<User>().AnyAsync(u => u.Email == req.AdminEmail && u.TenantId == null || u.Email == req.AdminEmail && _db.Tenants.Any(t=>t.Slug==req.Slug && t.Id==u.TenantId), ct);
        // Note: allow same email across tenants? For first admin, if email exists in other tenant, we allow new user record per tenant isolation. So check only platform.

        // One unit of work under the retrying execution strategy (see InTransactionAsync).
        var registered = await _db.InTransactionAsync(async () =>
        {
            var tenant = new Tenant
            {
                Name = req.SchoolName,
                Slug = req.Slug.ToLowerInvariant(),
                City = req.City,
                ContactEmail = req.ContactEmail,
                ContactPhone = req.ContactPhone,
                LearnerCountBand = req.LearnerCountBand,
                Status = "trial",
                PrimaryColor = "#0F153A"
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(ct); // gets Id

            // Every school starts a 14-day trial on the starter plan (PlatformBilling owns
            // plans and the subscription state machine). The plan is created on first use.
            var plan = await _db.Set<Plan>().FirstOrDefaultAsync(p => p.Code == "starter" && p.IsActive, ct);
            if (plan == null)
            {
                plan = new Plan
                {
                    Code = "starter", Name = "Starter", Description = "Up to 300 learners",
                    PricePerLearnerPerTerm = 2.00m, MinimumCharge = 99m, LearnerLimit = 300,
                    IncludedSmsBundle = 500, Currency = "USD", IsActive = true,
                    // Trials must be able to use every module; with the entity default "[]"
                    // feature gating blocked academic, fees and attendance right after signup.
                    IncludedModulesJson = System.Text.Json.JsonSerializer.Serialize(FeatureFlags.All)
                };
                _db.Set<Plan>().Add(plan);
                await _db.SaveChangesAsync(ct);
            }

            var trialStart = DateTime.UtcNow;
            var sub = new Subscription
            {
                TenantId = tenant.Id,
                PlanId = plan.Id,
                State = SubscriptionState.Trialing,
                TrialStartedAt = trialStart,
                TrialEndsAt = trialStart.AddDays(14),
                CurrentPeriodStart = trialStart,
                CurrentPeriodEnd = trialStart.AddDays(14),
                Currency = plan.Currency
            };
            _db.Set<Subscription>().Add(sub);

            // Admin user
            var admin = new User
            {
                TenantId = tenant.Id,
                Email = req.AdminEmail.ToLowerInvariant(),
                Phone = req.AdminPhone,
                DisplayName = req.AdminFullName,
                Status = "active",
                EmailVerified = false,
                SecurityStamp = Guid.NewGuid().ToString(),
                TokenVersion = 1
            };
            admin.PasswordHash = _hasher.HashPassword(admin, req.Password);
            _db.Set<User>().Add(admin);
            await _db.SaveChangesAsync(ct);

            // Assign SCHOOL_ADMIN role - ensure roles seeded
            var schoolAdminRole = await _db.Set<Role>().FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.Code == "SCHOOL_ADMIN", ct);
            if (schoolAdminRole == null)
            {
                // SECURITY H7 FIX: Validate role creation - prevent privilege escalation
                RoleSecurityGuard.ValidateRoleCreation("SCHOOL_ADMIN", tenant.Id);
                
                // Fallback seed from platform template or create
                schoolAdminRole = new Role { TenantId = tenant.Id, Code="SCHOOL_ADMIN", Name="School Admin", IsSystem=true, Description="IT Admin god" };
                _db.Set<Role>().Add(schoolAdminRole);
                await _db.SaveChangesAsync(ct);

                // Copy permissions from platform template? For demo, assign all tenant perms (excluding platform)
                // SECURITY H7: Only tenant permissions, never platform
                var tenantPerms = await _db.Set<Permission>().Where(p => !p.Code.StartsWith("platform.") && !p.Code.StartsWith("tenants.")).ToListAsync(ct);
                foreach (var perm in tenantPerms)
                {
                    RoleSecurityGuard.ValidatePermissionAssignment(perm.Code, tenant.Id);
                }
                foreach (var perm in tenantPerms)
                {
                    _db.Set<RolePermission>().Add(new RolePermission { TenantId = tenant.Id, RoleId = schoolAdminRole.Id, PermissionId = perm.Id });
                }
                await _db.SaveChangesAsync(ct);
            }

            _db.Set<UserRole>().Add(new UserRole { TenantId = tenant.Id, UserId = admin.Id, RoleId = schoolAdminRole.Id });
            await _db.SaveChangesAsync(ct);

            // Email verification token - single-use hashed, 24h
            var rawEmailToken = _tokenService.GenerateOpaqueToken(32);
            var hashedEmailToken = _tokenService.HashToken(rawEmailToken);
            var emailToken = new UserToken
            {
                TenantId = tenant.Id,
                UserId = admin.Id,
                TokenType = UserTokenType.EmailVerification,
                TokenHash = hashedEmailToken,
                ExpiresAt = DateTime.UtcNow.AddHours(_pwdPol.Value.EmailVerificationTokenHours)
            };
            _db.Set<UserToken>().Add(emailToken);
            await _db.SaveChangesAsync(ct);

            return (Response: new RegisterTenantResponse(tenant.Id, tenant.Slug, admin.Id, sub.TrialEndsAt!.Value.ToString("o"), true), Admin: admin, RawEmailToken: rawEmailToken);
        }, ct);

        // Email only after the commit succeeded. Never log tokens.
        _logger.LogInformation("Tenant {Slug} registered with admin {Email} from ip {Ip}", registered.Response.Slug, registered.Admin.Email, ip);
        await _emailSender.SendEmailVerificationAsync(registered.Admin.Email, registered.Admin.DisplayName, registered.RawEmailToken, registered.Response.TenantId);
        return registered.Response;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest req, string ip, CancellationToken ct = default)
    {
        // Constant-time flow: even if user not found, do dummy hash to keep timing similar
        User? user = null;
        long? tenantId = null;
        if (!string.IsNullOrEmpty(req.TenantSlug))
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == req.TenantSlug.ToLower() && !t.IsDeleted, ct);
            if (tenant != null)
            {
                tenantId = tenant.Id;
                user = await _db.Set<User>().Include(u=>u.UserRoles).ThenInclude(ur=>ur.Role)
                    .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower() && u.TenantId == tenant.Id && !u.IsDeleted, ct);
            }
        }
        else
        {
            // Platform login or infer: try tenant by email? For simplicity, search any tenant user, then platform
            user = await _db.Set<User>().Include(u=>u.UserRoles).ThenInclude(ur=>ur.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower() && u.TenantId != null && !u.IsDeleted, ct)
                ?? await _db.Set<User>().Include(u=>u.UserRoles).ThenInclude(ur=>ur.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower() && u.TenantId == null && !u.IsDeleted, ct);
            tenantId = user?.TenantId;
        }

        // Dummy hasher to keep timing profile same for valid/invalid
        var dummyUser = new User();
        var dummyHash = _hasher.HashPassword(dummyUser, "DummyPassword123!");

        if (user == null)
        {
            // Timing mitigation: verify dummy
            _hasher.VerifyHashedPassword(dummyUser, dummyHash, req.Password);
            await Task.Delay(RandomNumberGenerator.GetInt32(80, 220), ct); // jitter
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Lockout check
        if (user.IsLockedOut)
            throw new UnauthorizedAccessException($"Account locked until {user.LockoutEnd:u}. Contact School Admin to unlock.");

        if (user.Status == "disabled")
            throw new UnauthorizedAccessException("Account disabled");

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _pwdPol.Value.MaxFailedAccessAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(_pwdPol.Value.LockoutMinutes);
                _logger.LogWarning("User {UserId} locked out after {Count} attempts from {Ip}", user.Id, user.FailedLoginCount, ip);
            }
            await _db.SaveChangesAsync(ct);
            // Constant timing
            await Task.Delay(RandomNumberGenerator.GetInt32(80, 220), ct);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Success - reset failed count
        user.FailedLoginCount = 0;
        user.LastLoginAt = DateTime.UtcNow;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync(ct);

        var roleCodes = await _db.Set<UserRole>().Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
            .Join(_db.Set<Role>(), ur=>ur.RoleId, r=>r.Id, (ur,r)=>r.Code).ToListAsync(ct);

        var permCodes = await _db.Set<UserRole>().Where(ur=>ur.UserId==user.Id && !ur.IsDeleted)
            .Join(_db.Set<RolePermission>(), ur=>ur.RoleId, rp=>rp.RoleId, (ur,rp)=>rp.PermissionId)
            .Join(_db.Set<Permission>(), pid=>pid, p=>p.Id, (pid,p)=>p.Code).Distinct().ToListAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user, roleCodes, permCodes);

        // Refresh token - rotating, 14 days, family
        var (rawRefresh, hashedRefresh) = _tokenService.GenerateRefreshToken();
        var refreshEntity = new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = hashedRefresh,
            FamilyId = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.Value.RefreshTokenLifetimeDays),
            CreatedByIp = ip,
            Device = req.Device
        };
        _db.Set<RefreshToken>().Add(refreshEntity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} logged in from {Ip} tenant {TenantId}", user.Id, ip, user.TenantId);

        return new TokenResponse(accessToken, rawRefresh, DateTime.UtcNow.AddMinutes(_jwt.Value.AccessTokenLifetimeMinutes), refreshEntity.ExpiresAt, user.Id, user.TenantId, user.DisplayName);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest req, string ip, CancellationToken ct = default)
    {
        var hashed = _tokenService.HashToken(req.RefreshToken);
        var stored = await _db.Set<RefreshToken>().Include(rt=>rt.User).FirstOrDefaultAsync(rt=>rt.TokenHash==hashed, ct);

        if (stored == null)
        {
            // Token not found - possible reuse of already revoked? Try find by family to detect reuse
            // For security, we don't reveal, just unauthorized
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Reuse detection: if token already revoked or replaced, then attacker reused old token -> revoke whole family
        if (stored.IsRevoked || stored.IsExpired || stored.ReplacedByTokenId.HasValue)
        {
            // Revoke whole family
            var familyTokens = await _db.Set<RefreshToken>().Where(rt=>rt.FamilyId==stored.FamilyId && rt.UserId==stored.UserId && !rt.IsDeleted).ToListAsync(ct);
            foreach (var ft in familyTokens)
            {
                if (!ft.IsRevoked)
                {
                    ft.RevokedAt = DateTime.UtcNow;
                    ft.RevokedReason = "reuse_detected";
                }
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Refresh token reuse detected! Family {Family} user {UserId} revoked, ip {Ip}", stored.FamilyId, stored.UserId, ip);
            throw new UnauthorizedAccessException("Refresh token reuse detected, family revoked");
        }

        // Valid - rotate
        var user = stored.User;
        if (user.IsDeleted || user.Status=="disabled" || user.IsLockedOut)
            throw new UnauthorizedAccessException("User inactive");

        // Check token version still valid (revoked by password change)
        // If TokenVersion in JWT vs user mismatch, refresh should still check user.TokenVersion? Stored refresh doesn't contain version but user may have incremented version after password change -> all refresh should be revoked already

        var roleCodes = await _db.Set<UserRole>().Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
            .Join(_db.Set<Role>(), ur=>ur.RoleId, r=>r.Id, (ur,r)=>r.Code).ToListAsync(ct);
        var permCodes = await _db.Set<UserRole>().Where(ur=>ur.UserId==user.Id && !ur.IsDeleted)
            .Join(_db.Set<RolePermission>(), ur=>ur.RoleId, rp=>rp.RoleId, (ur,rp)=>rp.PermissionId)
            .Join(_db.Set<Permission>(), pid=>pid, p=>p.Id, (pid,p)=>p.Code).Distinct().ToListAsync(ct);

        var newAccess = _tokenService.GenerateAccessToken(user, roleCodes, permCodes);
        var (newRaw, newHashed) = _tokenService.GenerateRefreshToken();

        var newRefresh = new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = newHashed,
            FamilyId = stored.FamilyId,
            ParentTokenId = stored.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.Value.RefreshTokenLifetimeDays),
            CreatedByIp = ip,
            Device = req.Device
        };

        // Mark old as replaced
        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedReason = "rotated";
        _db.Set<RefreshToken>().Add(newRefresh);
        await _db.SaveChangesAsync(ct);

        // Update ReplacedBy
        stored.ReplacedByTokenId = newRefresh.Id;
        await _db.SaveChangesAsync(ct);

        return new TokenResponse(newAccess, newRaw, DateTime.UtcNow.AddMinutes(_jwt.Value.AccessTokenLifetimeMinutes), newRefresh.ExpiresAt, user.Id, user.TenantId, user.DisplayName);
    }

    public async Task<EmailVerificationResponse> VerifyEmailAsync(VerifyEmailRequest req, CancellationToken ct = default)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u=>u.Email==req.Email.ToLower() && !u.IsDeleted, ct);
        if (user == null) throw new InvalidOperationException("Invalid token"); // same message for security

        var hashed = _tokenService.HashToken(req.Token);
        var token = await _db.Set<UserToken>().FirstOrDefaultAsync(t=>t.UserId==user.Id && t.TokenType==UserTokenType.EmailVerification && t.TokenHash==hashed && !t.IsDeleted, ct);
        if (token == null || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired token");

        token.UsedAt = DateTime.UtcNow;
        user.EmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.Status = "active";
        await _db.SaveChangesAsync(ct);

        return new EmailVerificationResponse(true, "Email verified");
    }

    // Security: never reveal whether email exists, always return same message & timing
    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest req, string ip, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            User? user = null;
            if (!string.IsNullOrEmpty(req.TenantSlug))
            {
                var tenant = await _db.Tenants.FirstOrDefaultAsync(t=>t.Slug==req.TenantSlug.ToLower() && !t.IsDeleted, ct);
                if (tenant != null)
                    user = await _db.Set<User>().FirstOrDefaultAsync(u=>u.Email==req.Email.ToLower() && u.TenantId==tenant.Id && !u.IsDeleted, ct);
            }
            else
            {
                user = await _db.Set<User>().FirstOrDefaultAsync(u=>u.Email==req.Email.ToLower() && !u.IsDeleted, ct);
            }

            if (user != null)
            {
                // Invalidate old reset tokens
                var oldTokens = await _db.Set<UserToken>().Where(t=>t.UserId==user.Id && t.TokenType==UserTokenType.PasswordReset && !t.IsUsed && !t.IsDeleted).ToListAsync(ct);
                foreach (var ot in oldTokens) ot.IsDeleted=true;

                var raw = _tokenService.GenerateOpaqueToken(32);
                var hashed = _tokenService.HashToken(raw);
                var ut = new UserToken
                {
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    TokenType = UserTokenType.PasswordReset,
                    TokenHash = hashed,
                    ExpiresAt = DateTime.UtcNow.AddHours(_pwdPol.Value.PasswordResetTokenHours)
                };
                _db.Set<UserToken>().Add(ut);
                await _db.SaveChangesAsync(ct);

                // Never log tokens!
                _logger.LogInformation("Password reset requested for {Email} tenant {Tenant} ip {Ip}", req.Email, user.TenantId, ip);
                await _emailSender.SendPasswordResetAsync(user.Email, user.DisplayName, raw, user.TenantId);
            }
            else
            {
                // Dummy delay to keep timing profile same as valid user
                _logger.LogInformation("Password reset requested for non-existent {Email} ip {Ip}", req.Email, ip);
            }
        }
        finally
        {
            // Ensure same timing ~ 400-700ms
            var elapsed = sw.ElapsedMilliseconds;
            var targetMin = 500;
            if (elapsed < targetMin)
                await Task.Delay(TimeSpan.FromMilliseconds(targetMin - elapsed + RandomNumberGenerator.GetInt32(0,150)), ct);
        }

        return new ForgotPasswordResponse("If an account exists with that email, a reset link has been sent. Check spam folder.");
    }

    public async Task<EmailVerificationResponse> ResetPasswordAsync(ResetPasswordRequest req, string ip, CancellationToken ct = default)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u=>u.Email==req.Email.ToLower() && !u.IsDeleted, ct);
        if (user == null) throw new InvalidOperationException("Invalid token");

        var hashed = _tokenService.HashToken(req.Token);
        var token = await _db.Set<UserToken>().FirstOrDefaultAsync(t=>t.UserId==user.Id && t.TokenType==UserTokenType.PasswordReset && t.TokenHash==hashed && !t.IsDeleted, ct);
        if (token == null || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired token");

        // Policy check
        if (req.NewPassword.Length < _pwdPol.Value.RequiredLength)
            throw new InvalidOperationException("Password does not meet policy");

        // Change password + revoke all refresh tokens + increment token version
        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.TokenVersion++;
        token.UsedAt = DateTime.UtcNow;

        // Revoke all refresh
        var refreshes = await _db.Set<RefreshToken>().Where(rt=>rt.UserId==user.Id && rt.RevokedAt==null && !rt.IsDeleted).ToListAsync(ct);
        foreach (var rt in refreshes)
        {
            rt.RevokedAt = DateTime.UtcNow;
            rt.RevokedReason = "password_reset";
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Password reset completed for user {UserId} ip {Ip}", user.Id, ip);
        return new EmailVerificationResponse(true, "Password reset successful");
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest req, CancellationToken ct = default)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u=>u.Id==userId && !u.IsDeleted, ct) ?? throw new InvalidOperationException("User not found");
        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Current password invalid");

        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.TokenVersion++;
        user.MustChangePassword = false;

        var refreshes = await _db.Set<RefreshToken>().Where(rt=>rt.UserId==user.Id && rt.RevokedAt==null && !rt.IsDeleted).ToListAsync(ct);
        foreach (var rt in refreshes)
        {
            rt.RevokedAt = DateTime.UtcNow;
            rt.RevokedReason = "password_changed";
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {UserId} changed password, revoked {Count} refresh tokens", userId, refreshes.Count);
    }

    public async Task RevokeAllRefreshTokensAsync(long userId, string reason, CancellationToken ct = default)
    {
        var tokens = await _db.Set<RefreshToken>().Where(rt=>rt.UserId==userId && rt.RevokedAt==null && !rt.IsDeleted).ToListAsync(ct);
        foreach (var t in tokens)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedReason = reason;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserInfoDto> GetUserInfoAsync(long userId, long? tenantId, CancellationToken ct = default)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u=>u.Id==userId && (tenantId==null || u.TenantId==tenantId) && !u.IsDeleted, ct) ?? throw new InvalidOperationException("User not found");
        var roles = await _db.Set<UserRole>().Where(ur=>ur.UserId==user.Id && !ur.IsDeleted).Join(_db.Set<Role>(), ur=>ur.RoleId, r=>r.Id, (ur,r)=>r.Code).ToListAsync(ct);
        var perms = await _db.Set<UserRole>().Where(ur=>ur.UserId==user.Id && !ur.IsDeleted).Join(_db.Set<RolePermission>(), ur=>ur.RoleId, rp=>rp.RoleId, (ur,rp)=>rp.PermissionId).Join(_db.Set<Permission>(), pid=>pid, p=>p.Id, (pid,p)=>p.Code).Distinct().ToListAsync(ct);
        return new UserInfoDto(user.Id, user.TenantId, user.Email, user.DisplayName, user.EmailVerified, user.Status, user.TokenVersion, user.LastLoginAt, roles, perms);
    }

    // Session management. A "session" is one refresh-token family: rotation
    // replaces the token but keeps FamilyId, so the family is the device.
    // Declared on IAuthService but never implemented, which broke the build.

    public async Task<List<UserSessionDto>> GetActiveSessionsAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var live = await _db.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > now && !rt.IsDeleted)
            .ToListAsync(ct);

        // One row per family: the newest token carries the current device and IP.
        return live
            .GroupBy(rt => rt.FamilyId)
            .Select(g => g.OrderByDescending(rt => rt.CreatedAt).First())
            .OrderByDescending(rt => rt.CreatedAt)
            .Select(rt => new UserSessionDto(
                rt.FamilyId,
                rt.Device ?? "unknown",
                rt.CreatedByIp,
                rt.CreatedAt,
                rt.ExpiresAt,
                // Which family is the caller's own cannot be known here; the
                // interface takes no current-token argument. The controller
                // marks it from the presented refresh cookie.
                string.Empty))
            .ToList();
    }

    public async Task RevokeSessionAsync(long userId, string familyId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(familyId)) throw new ArgumentException("familyId is required", nameof(familyId));

        var family = await _db.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId && rt.FamilyId == familyId && rt.RevokedAt == null && !rt.IsDeleted)
            .ToListAsync(ct);

        if (family.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var rt in family)
        {
            rt.RevokedAt = now;
            rt.RevokedReason = reason;
        }
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Revoked session family {Family} for user {UserId}, reason {Reason}", familyId, userId, reason);
    }

    public async Task EnforceMaxSessionsAsync(long userId, int maxFamilies = 5, CancellationToken ct = default)
    {
        if (maxFamilies < 1) throw new ArgumentOutOfRangeException(nameof(maxFamilies), "At least one session must be allowed.");

        var now = DateTime.UtcNow;
        var live = await _db.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > now && !rt.IsDeleted)
            .ToListAsync(ct);

        var families = live
            .GroupBy(rt => rt.FamilyId)
            .Select(g => new { FamilyId = g.Key, LastSeen = g.Max(rt => rt.CreatedAt), Tokens = g.ToList() })
            .OrderByDescending(f => f.LastSeen)
            .ToList();

        if (families.Count <= maxFamilies) return;

        // Keep the most recently used families, revoke the rest.
        foreach (var stale in families.Skip(maxFamilies))
        {
            foreach (var rt in stale.Tokens)
            {
                rt.RevokedAt = now;
                rt.RevokedReason = "max_sessions_exceeded";
            }
            _logger.LogInformation("Max sessions ({Max}) exceeded for user {UserId}; revoked family {Family}", maxFamilies, userId, stale.FamilyId);
        }
        await _db.SaveChangesAsync(ct);
    }
}

public class FakeEmailSender : IEmailSender
{
    private readonly ILogger<FakeEmailSender> _logger;
    public FakeEmailSender(ILogger<FakeEmailSender> logger){ _logger=logger; }
    public Task SendEmailVerificationAsync(string email, string displayName, string rawToken, long? tenantId)
    {
        _logger.LogInformation("[FAKE EMAIL] Verification to {Email} token length {Len} tenant {Tenant} - link: https://{Tenant}.learncloud.co.zw/verify?email={Email}&token={Token} **NEVER LOG TOKEN IN PROD**", email, rawToken.Length, tenantId, tenantId, email, "[REDACTED]");
        // In dev, you could log partially but spec says never log tokens - we redact
        return Task.CompletedTask;
    }
    public Task SendPasswordResetAsync(string email, string displayName, string rawToken, long? tenantId)
    {
        _logger.LogInformation("[FAKE EMAIL] Reset to {Email} tenant {Tenant} link https://learncloud.co.zw/reset?email={Email}&token=[REDACTED]", email, tenantId, email);
        return Task.CompletedTask;
    }
    public Task SendWelcomeAsync(string email, string displayName, long? tenantId)
    {
        _logger.LogInformation("[FAKE EMAIL] Welcome to {Email}", email);
        return Task.CompletedTask;
    }
}
