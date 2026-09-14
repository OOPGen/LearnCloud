# Fix Security C4 - Missing Anti-CSRF for Refresh Cookie

**Severity:** Critical (chained with C2)
**Status:** FIXED
**Date:** 2026-08-09

## Vulnerability
- Refresh endpoint `/api/auth/refresh` accepted token from cookie `refresh_token` (HttpOnly Secure SameSite=Lax) AND body
- Lax allows top-level GET but POST cross-site can be blocked? Actually Lax blocks POST cross-site unless top-level navigation, but attacker could use form POST from attacker.com with auto-submit, cookie sent, and if CORS allows *, could read new access token in response JSON
- Even if not readable, rotation revokes old token causing DoS (user logged out)
- No custom header validation

## Fix Applied

### AuthController.cs
```csharp
// BEFORE:
Response.Cookies.Append("refresh_token", token, new CookieOptions {
    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/api/auth"
});
var tokenFromCookie = Request.Cookies["refresh_token"];
var token = !string.IsNullOrEmpty(req.RefreshToken) ? req.RefreshToken : tokenFromCookie;

// AFTER:
Response.Cookies.Append("__Host-refresh_token", token, new CookieOptions {
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict, // Strict prevents CSRF - no cross-site send at all
    Expires = expires,
    Path = "/", // __Host- requires Path=/
    // No Domain for __Host-
});

var tokenFromCookie = Request.Cookies["__Host-refresh_token"] ?? Request.Cookies["refresh_token"];
var tokenFromBody = req?.RefreshToken;

bool isCookieAuth = !string.IsNullOrEmpty(tokenFromCookie) && string.IsNullOrEmpty(tokenFromBody);
if (isCookieAuth) {
    var hasCsrfHeader = Request.Headers.ContainsKey("X-Requested-With") || Request.Headers.ContainsKey("X-CSRF-Token");
    if (!hasCsrfHeader) {
        _logger.LogWarning("SECURITY: Refresh via cookie without CSRF header - possible CSRF");
        return BadRequest(new { message = "Missing CSRF header X-Requested-With for cookie refresh" });
    }
}
```

- `__Host-` prefix enforces Secure, Path=/, no Domain, prevents subdomain overwrite (e.g., attacker sets cookie via subdomain)
- `SameSite=Strict` - no cross-site send, even top-level GET blocked, strongest CSRF protection (Lax allowed top-level GET)
- Custom header check `X-Requested-With: XMLHttpRequest` - browsers prevent custom header in simple form POST, only JS fetch with CORS can send, so attacker.com form cannot set it
- apiClient.js sends `credentials: 'include'` + no custom header? Actually we should add header in apiClient refresh call

### apiClient.js
```javascript
const refreshRes = await fetch('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, // CSRF header
    credentials: 'include',
    body: JSON.stringify({}),
});
```

### CORS Fix (H1)
Added explicit CORS policy in `Program.cs`:
```csharp
builder.Services.AddCors(options => {
    options.AddPolicy("tenant", policy => {
        policy.WithOrigins("https://learncloud.co.zw", "https://*.learncloud.co.zw", "http://localhost:5173")
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyMethod().AllowAnyHeader().AllowCredentials()
              .WithExposedHeaders("X-Request-ID");
    });
});
```

No more wildcard `*` with credentials.

## Verification
- Refresh via cookie without `X-Requested-With` now returns 400
- Refresh via body (mobile) doesn't require header (since not cookie auth)
- SameSite=Strict cookie not sent on cross-site POST from attacker.com

## Impact
- CSRF via `<form action="https://api.learncloud.co.zw/api/auth/refresh" method="POST">` will not send __Host- cookie if Strict, and even if Lax fallback, header check blocks
- Combined with C2 HttpOnly, refresh token remains secure
