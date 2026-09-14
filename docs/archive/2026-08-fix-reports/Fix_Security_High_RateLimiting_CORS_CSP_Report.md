# Fix Security High - Rate Limiting, CORS, CSP

**Date:** 2026-08-09
**Status:** FIXED (H1, H2, H3)

## H1 CORS
**Before:** No CORS policy defined in Program.cs, potential wildcard.
**Fix:** Added explicit CORS policy in `Program.cs`:
```csharp
WithOrigins("https://learncloud.co.zw", "https://*.learncloud.co.zw", "http://localhost:5173")
AllowCredentials, WithExposedHeaders X-Request-ID, SetIsOriginAllowedToAllowWildcardSubdomains
```
Applied via `RequireCors("tenant")` on controllers and health endpoint.

## H2 CSP
**Before:** No Content-Security-Policy header, only X-Frame-Options, X-Content-Type-Options, deprecated X-XSS-Protection.
**Fix:**
- Created `SecurityHeadersMiddleware.cs` with CSP: `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self' https://*.learncloud.co.zw; object-src 'none'; base-uri 'self'; frame-ancestors 'self'; upgrade-insecure-requests;`
- Added X-Content-Type-Options nosniff, X-Frame-Options SAMEORIGIN, Referrer-Policy strict-origin-when-cross-origin, Permissions-Policy, X-Request-ID correlation, Cache-Control no-store for API, HSTS when https
- Removed deprecated X-XSS-Protection or set to 0
- Updated Nginx `nginx.conf` with same CSP header including `https://cdn.tailwindcss.com https://unpkg.com` for marketing site

## H3 Missing Rate Limits
**Before:** Only login 5/m, registration 3/h, password_reset 3/h. Missing refresh (brute force token hash), verify-email (spam), general API (DoS)
**Fix:** Added policies:
- `refresh`: 10/m per user/IP
- `verify_email`: 10/h per IP
- `api_general`: 60/m per user/IP
- `sensitive`: 20/m per user/IP

Applied:
- `POST /api/auth/refresh` -> refresh policy (was none)
- `POST /api/auth/verify-email` -> verify_email (was none)
- `POST /api/auth/reset-password` -> password_reset (was none, now added)
- General API should use `api_general` via base controller [EnableRateLimiting]

## Verification
```bash
grep -n "EnableRateLimiting" src/LearnCloud.Auth/Controllers/AuthController.cs
# login, registration, password_reset, refresh, verify_email, reset-password now all rate limited
```

## Remaining High
- H4 Argon2id - need custom IPasswordHasher
- H7 Role escalation - need guard for PLATFORM_* roles
- H8 Session fixation - device limit, sessions list
