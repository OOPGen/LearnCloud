using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformBilling.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformBilling.Middleware;

// Feature gating: middleware or filter that blocks endpoints belonging to modules not included in tenant's plan, returning clear upgrade-required response
public class FeatureGatingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FeatureGatingMiddleware> _logger;

    // Map endpoints to required feature flags
    private static readonly Dictionary<string, string> EndpointFeatureMap = new()
    {
        { "/api/students", FeatureFlags.Students },
        { "/api/guardians", FeatureFlags.Guardians },
        { "/api/staff", FeatureFlags.Staff },
        { "/api/attendance", FeatureFlags.Attendance },
        { "/api/timetable", FeatureFlags.Timetable },
        { "/api/fees", FeatureFlags.Fees },
        { "/api/assessments", FeatureFlags.Assessments },
        { "/api/marks", FeatureFlags.Assessments },
        { "/api/report-cards", FeatureFlags.ReportCards },
        { "/api/messaging", FeatureFlags.Messaging },
        { "/api/reports", FeatureFlags.Reports },
        { "/api/academic", FeatureFlags.Academic },
        { "/api/admissions", FeatureFlags.Admissions },
    };

    public FeatureGatingMiddleware(RequestDelegate next, ILogger<FeatureGatingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, LearnCloudDbContext db, ITenantContext tenantContext)
    {
        var path = httpContext.Request.Path.Value?.ToLower() ?? "";

        // Skip platform admin, billing, auth, health endpoints
        if (path.StartsWith("/api/platform") || path.StartsWith("/api/billing") || path.StartsWith("/api/auth") || path.StartsWith("/health") || path.StartsWith("/api/setup"))
        {
            await _next(httpContext);
            return;
        }

        // Find required feature for this endpoint
        var requiredFeature = EndpointFeatureMap.FirstOrDefault(kv => path.StartsWith(kv.Key)).Value;
        if (string.IsNullOrEmpty(requiredFeature))
        {
            await _next(httpContext);
            return;
        }

        var tenantId = tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            // Platform admin or no tenant, allow (feature gating only for tenant)
            await _next(httpContext);
            return;
        }

        // Get subscription and plan
        var subscription = await db.Set<Subscription>().Include(s => s.Plan).FirstOrDefaultAsync(s => s.TenantId == tenantId.Value && !s.IsDeleted);
        if (subscription == null)
        {
            await _next(httpContext);
            return;
        }

        var includedModules = new List<string>();
        try
        {
            includedModules = System.Text.Json.JsonSerializer.Deserialize<List<string>>(subscription.Plan.IncludedModulesJson) ?? new List<string>();
        }
        catch
        {
            includedModules = FeatureFlags.All.ToList(); // fallback allow all if deserialization fails
        }

        if (!includedModules.Contains(requiredFeature))
        {
            _logger.LogWarning("Feature gating blocked tenant {TenantId} accessing {Path} requires {Feature} not in plan {Plan}", tenantId, path, requiredFeature, subscription.Plan.Code);

            httpContext.Response.StatusCode = 402; // Payment Required
            httpContext.Response.ContentType = "application/json";
            var response = new
            {
                error = "upgrade_required",
                message = $"Your current plan '{subscription.Plan.Name}' does not include module '{requiredFeature}'. Upgrade required to access {path}.",
                requiredFeature,
                currentPlan = subscription.Plan.Code,
                currentPlanName = subscription.Plan.Name,
                upgradeUrl = $"/billing/upgrade?feature={requiredFeature}",
                includedModules
            };
            await httpContext.Response.WriteAsJsonAsync(response);
            return;
        }

        await _next(httpContext);
    }
}

public static class FeatureGatingExtensions
{
    public static IApplicationBuilder UseFeatureGating(this IApplicationBuilder app)
    {
        return app.UseMiddleware<FeatureGatingMiddleware>();
    }
}

// Alternative attribute for controller level
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresFeatureAttribute : Attribute, Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter
{
    private readonly string _feature;

    public RequiresFeatureAttribute(string feature) => _feature = feature;

    public async Task OnAuthorizationAsync(Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext context)
    {
        var tenantContext = context.HttpContext.RequestServices.GetService(typeof(ITenantContext)) as ITenantContext;
        var db = context.HttpContext.RequestServices.GetService(typeof(LearnCloudDbContext)) as LearnCloudDbContext;

        if (tenantContext?.TenantId == null)
        {
            return; // platform, allow
        }

        var sub = await db!.Set<Subscription>().Include(s => s.Plan).FirstOrDefaultAsync(s => s.TenantId == tenantContext.TenantId.Value && !s.IsDeleted);
        if (sub == null) return;

        var included = System.Text.Json.JsonSerializer.Deserialize<List<string>>(sub.Plan.IncludedModulesJson) ?? new List<string>();
        if (!included.Contains(_feature))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new
            {
                error = "upgrade_required",
                message = $"Plan {sub.Plan.Name} does not include {_feature}. Upgrade required.",
                requiredFeature = _feature,
                upgradeUrl = $"/billing/upgrade?feature={_feature}"
            })
            { StatusCode = 402 };
        }
    }
}
