using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformAdmin.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformAdmin.Middleware;

// The platform console can suspend a school and impersonate its users, so it asks for a code
// from the operator's authenticator as well as the platform role. The step-up is recorded in
// the database rather than a session: the API runs on more than one replica, and reading
// HttpContext.Session here (with no session configured) made every platform request fail
// with 500 - the console could not be used at all.
public class SecondFactorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecondFactorMiddleware> _logger;

    public SecondFactorMiddleware(RequestDelegate next, ILogger<SecondFactorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, LearnCloudDbContext db)
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

        var userId = long.TryParse(user.FindFirst("uid")?.Value ?? user.FindFirst("sub")?.Value, out var uid) ? uid : 0;
        var stepUpUntil = userId == 0 ? null : await db.Set<PlatformSecondFactor>()
            .Where(f => f.UserId == userId && !f.IsDeleted)
            .Select(f => f.StepUpUntil)
            .FirstOrDefaultAsync();

        if (stepUpUntil is null || stepUpUntil <= DateTime.UtcNow)
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
