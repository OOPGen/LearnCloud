using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Services;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformBilling.Middleware;

// Blocks, with 402:
// - endpoints of modules the school's plan does not include (upgrade_required);
// - changes by a school whose subscription is read-only (account_read_only). Reading is
//   never blocked, and signing in, billing and platform endpoints stay open.
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

    // POST endpoints that only read (exports and previews); read-only schools keep them.
    private static readonly HashSet<string> ReadingPosts = new()
    {
        "/api/hr/payroll-export",
        "/api/messaging/preview",
        "/api/communication/segments/preview",
    };

    public FeatureGatingMiddleware(RequestDelegate next, ILogger<FeatureGatingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, LearnCloudDbContext db, ITenantContext tenantContext, IOptions<BillingOptions> billing)
    {
        var path = httpContext.Request.Path.Value?.ToLower() ?? "";

        // Skip platform admin, billing, auth, health and public endpoints
        if (path.StartsWith("/api/platform") || path.StartsWith("/api/billing") || path.StartsWith("/api/auth") || path.StartsWith("/health") || path.StartsWith("/api/health") || path.StartsWith("/api/public"))
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

        var requiredFeature = EndpointFeatureMap.FirstOrDefault(kv => path.StartsWith(kv.Key)).Value;
        var isChange = !(HttpMethods.IsGet(httpContext.Request.Method) || HttpMethods.IsHead(httpContext.Request.Method) || HttpMethods.IsOptions(httpContext.Request.Method))
            && !(HttpMethods.IsPost(httpContext.Request.Method) && ReadingPosts.Contains(path.TrimEnd('/')));
        if (string.IsNullOrEmpty(requiredFeature) && !(isChange && billing.Value.EnforceReadOnly))
        {
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

        if (isChange && billing.Value.EnforceReadOnly && SubscriptionAccess.IsReadOnly(subscription.State))
        {
            _logger.LogInformation("Read-only account: blocked {Method} {Path} for tenant {TenantId} in state {State}", httpContext.Request.Method, path, tenantId, subscription.State);
            httpContext.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "account_read_only",
                title = "Your school is read-only",
                detail = SubscriptionAccess.Banner(subscription, billing.Value.ContactEmail),
                state = subscription.State.ToString(),
                contactEmail = billing.Value.ContactEmail,
            });
            return;
        }

        if (string.IsNullOrEmpty(requiredFeature))
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
