# LearnCloud Authentication Module V1
**HQ:** Bulawayo, ZW | **Stack:** ASP.NET Core 8, EF Core MySQL, JWT, Identity Hasher, FluentValidation, RateLimiting

## Features Delivered

### 1. School Self-Registration Transactional
`POST /api/auth/register-tenant` creates:
- `tenants` row slug unique
- `tenant_subscriptions` 14-day trial (starter plan) period start/end
- `users` admin with PasswordHasher<User> (ASP.NET Identity v3)
- `roles` SCHOOL_ADMIN if not seeded + `role_permissions` all tenant perms (excluding platform)
- `user_roles` link
- `user_tokens` EmailVerification hashed single-use 24h
All within one DB transaction `BeginTransactionAsync`. If any fails, rollback.

### 2. Login + JWT (15m) + Rotating Refresh (14d) Hashed, Reuse Detection
- Login: checks lockout, verifies hasher, constant-time dummy hash for invalid user (same timing profile), jitter delay 80-220ms
- Returns `TokenResponse` accessToken (JWT) + opaque refresh token (64 bytes base64url)
- Refresh token stored HASHED SHA256 hex, never raw
- Refresh flow: find by hash, if revoked/expired/replaced => reuse detection => revoke whole family (all tokens with same FamilyId) reason `reuse_detected`, log warning
- Rotation: old token revoked `rotated`, new token FamilyId same, ParentTokenId = old Id, ReplacedBy = new Id
- Refresh token also HttpOnly Secure cookie `refresh_token` path /api/auth

JWT claims:
- `uid` (user id), `tid` (tenant id, 0=platform), `roles` comma, `perm` repeated, `perms` space-joined if <=80 else `perms_hash` + `perms_ref=db`, `tv` tokenVersion, `ss` securityStamp, `jti`, `sub`, `email_verified`

### 3. Email Verification, Forgot/Reset - single-use hashed tokens
- Email verification: opaque token 32 bytes, hash stored, 24h expiry, UsedAt null check, single-use
- Forgot password: `POST /api/auth/forgot-password` never reveals existence - always returns same message "If an account exists..." with same timing profile (artificial delay min 500ms + jitter)
- Reset token: 2h expiry, single-use, hashed
- Both tokens: `user_tokens` table tenant_id, user_id, token_type, token_hash, expires_at, used_at, is_deleted
- Security: never log tokens! FakeEmailSender logs [REDACTED]

### 4. Change Password Revokes All Refresh
- `POST /api/auth/change-password` (Authorized) verifies current password, hashes new, increments TokenVersion and new SecurityStamp, revokes all refresh tokens `password_changed`, clears cookie, requires re-login

### 5. Password Hashing via ASP.NET Identity Hasher
- `IPasswordHasher<User>` with configurable policy via PasswordPolicyOptions (length 8, upper, lower, digit, non-alnum)
- FluentValidation also enforces complexity
- Policy in appsettings.json configurable

### 6. Permission-Based Authorization
- `RequiresPermissionAttribute` : TypeFilterAttribute wrapping `PermissionFilter`
- `PermissionRequirement` + `PermissionAuthorizationHandler` : checks JWT `perm` claims fast path, else loads from DB via roles, validates TokenVersion and SecurityStamp mismatch => fail, logs
- Row-level scoping intended to be applied in repository after tenant filter (not in this module but handler ensures user belongs to tenant)
- Example in AuthController: `[RequiresPermission("students.read")]`

### 7. Rate Limiting
- `AddRateLimiter` with partitions per IP: `login` 5/min, `registration` 3/hour, `password_reset` 3/hour
- Applied via `[EnableRateLimiting("login")]` on login, registration, forgot, change
- Returns 429

### 8. Lockout
- User.FailedLoginCount increments on bad password, after MaxFailedAttempts (5) sets LockoutEnd = now + LockoutMinutes (15)
- IsLockedOut property check in login, returns 401 "Account locked until..."
- Unlock path: `POST /api/auth/unlock/{userId}` requires `users.disable` permission (school admin), clears count and lockoutEnd
- Also auto unlock after time passes

## Entities & EF Config
- All tables have audit columns per schema spec: id BIGINT UNSIGNED, created_at, created_by, updated_at, updated_by, is_deleted, deleted_at, deleted_by
- Indexes lead with tenant_id: `uq_users_tenant_email (tenant_id, email)`, `idx_refresh_tenant_user_family (tenant_id, user_id, family_id)` etc
- RefreshTokens: FamilyId for reuse detection, ParentTokenId, ReplacedByTokenId, RevokedReason

## Migration
- `Migrations/V1_Auth_Migration.sql` full MySQL script
- EF migrations can be generated: `dotnet ef migrations add V1_Auth`

## DTOs & Validators
- DTOs in `DTOs/AuthDTOs.cs` - transactional request/response
- Validators use FluentValidation with complexity checks

## Services
- `TokenService`: JWT creation, opaque token generation via RandomNumberGenerator, SHA256 hash
- `AuthService`: transactional registration, login constant-time, refresh with reuse detection family revoke, forgot same timing, reset increments tokenVersion
- `FakeEmailSender`: logs redacted

## Controllers
- `AuthController` routes: register-tenant, login, refresh, logout, verify-email, forgot-password, reset-password, change-password, me, students (example with RequiresPermission), unlock

## DI Registration
- `Extensions/AuthModuleExtensions.cs` AddLearnCloudAuth() - reads Jwt, PasswordPolicy, RateLimit from config, adds DbContext MySQL, hasher, token service, auth service, validators, JWT auth with events (never log tokens), Authorization handler, RateLimiter, controllers

## Integration Tests
- `tests/AuthIntegrationTests.cs` xUnit WebApplicationFactory InMemory
- Covers: registration transactional, login JWT+refresh, refresh rotates old invalid, reuse detection revokes family, forgot never reveals existence same timing, reset with valid token and revokes refresh, lockout after 5 fails and unlock path, change password revokes all

## Security Notes Implemented
- Forgot-password same message & timing profile for valid/invalid users (500ms min + jitter, dummy delay)
- Never log tokens - all email sender logs [REDACTED], JWT OnAuthenticationFailed logs only message not token
- Refresh tokens hashed SHA256, stored hashed only
- Single-use hashed tokens for email verification and reset with expiry
- Token version and security stamp in JWT for instant revocation on password change
- Rate limiting per IP
- Constant-time password verification with dummy hasher for non-existent user

## How to Run
```
dotnet add package Microsoft.AspNetCore.Identity
dotnet add package FluentValidation.AspNetCore
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Pomelo.EntityFrameworkCore.MySql
dotnet ef database update
dotnet run
```

## Next Steps
- Wire to real email via Mailgun/SendGrid in IEmailSender production implementation
- Add break-glass audit for platform superadmin viewing tenant PII
- Add device fingerprinting for refresh tokens
- Add 2FA SHOULD for V2
