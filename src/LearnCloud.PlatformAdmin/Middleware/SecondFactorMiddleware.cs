using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.PlatformAdmin.Middleware;

// Access requires platform superadmin role plus second factor
public class SecondFactorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecondFactorMiddleware> _logger;

    public SecondFactorMiddleware(RequestDelegate next, ILogger<SecondFactorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to platform admin routes
        if (!context.Request.Path.StartsWithSegments("/api/platform") && !context.Request.Path.StartsWithSegments("/api/platform/console"))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        // Check if user is PLATFORM_SUPERADMIN
        var isPlatformSuperAdmin = user.IsInRole("PLATFORM_SUPERADMIN") || user.Claims.Any(c => c.Type == "roles" && c.Value == "PLATFORM_SUPERADMIN") || user.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "PLATFORM_SUPERADMIN");

        if (!isPlatformSuperAdmin)
        {
            await _next(context);
            return;
        }

        // For platform superadmin, require second factor verified in session
        // Allow second-factor verify endpoint itself without 2FA
        if (context.Request.Path.Value?.Contains("second-factor/verify") == true)
        {
            await _next(context);
            return;
        }

        var is2FaVerified = context.Session.GetString("2fa_verified") == "true";

        if (!is2FaVerified)
        {
            _logger.LogWarning("Platform superadmin {User} attempted to access {Path} without second factor", user.Identity?.Name, context.Request.Path);
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "second_factor_required",
                message = "Access to platform admin console requires platform superadmin role plus second factor (TOTP). Please verify second factor via POST /api/platform/second-factor/verify {code}. Impersonation without consent impossible by design.",
                verifyUrl = "/api/platform/second-factor/verify"
            });
            return;
        }

        await _next(context);
    }
}

public static class SecondFactorExtensions
{
    public static IApplicationBuilder UseSecondFactor(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecondFactorMiddleware>();
    }
}
