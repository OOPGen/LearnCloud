using Microsoft.AspNetCore.Http;

namespace LearnCloud.Api.Middleware;

// SECURITY: H2 + C2 + C4 - Security headers including CSP, HSTS, etc.
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // SECURITY H2: Content-Security-Policy - prevents XSS
        // For API: restrictive, no inline scripts
        // For marketing may need more permissive, but API strict
        // Allow self only for API, plus specific CDNs for web if needed via meta tag in HTML (not header for API)
        var csp = "default-src 'self'; " +
                  "script-src 'self'; " + // No unsafe-inline, no unpkg for API - web uses separate CSP via Nginx meta
                  "style-src 'self' 'unsafe-inline'; " + // unsafe-inline needed for some, but better to use nonce in future
                  "img-src 'self' data: https:; " +
                  "font-src 'self'; " +
                  "connect-src 'self' https://*.learncloud.co.zw https://api.learncloud.co.zw; " +
                  "object-src 'none'; " +
                  "base-uri 'self'; " +
                  "frame-ancestors 'self'; " +
                  "form-action 'self'; " +
                  "upgrade-insecure-requests;";

        // Only add CSP for API responses (not for static assets that may need more permissive via Nginx)
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.Headers["Content-Security-Policy"] = csp;
        }

        // Other security headers (defense in depth, also set in Nginx but also here for non-Nginx deployments)
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        context.Response.Headers["X-Request-ID"] = context.TraceIdentifier; // M3 fix - correlation ID
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate"; // for API, prevent caching of sensitive data

        // Remove deprecated X-XSS-Protection or set to 0 (modern browsers ignore, CSP is actual protection)
        context.Response.Headers["X-XSS-Protection"] = "0";

        // HSTS will be set by Nginx for TLS, but also here for direct API access
        if (context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
