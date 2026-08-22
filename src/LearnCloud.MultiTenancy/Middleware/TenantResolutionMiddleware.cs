using System.Security.Claims;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LearnCloud.MultiTenancy.Middleware;

// 3. ITenantContext and implementation resolves per request
// 4. Tenant resolution middleware, ordered correctly relative to authentication, returns 403 and logs security event if subdomain tenant and token tenant disagree

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, ITenantContext tenantContext, LearnCloudDbContext db)
    {
        var host = httpContext.Request.Host.Host.ToLowerInvariant(); // e.g. petra.learncloud.co.zw or localhost
        long? subdomainTenantId = null;
        Tenant? subdomainTenant = null;

        // 3. For anonymous requests (login, registration) resolves tenant from subdomain purely to select login context
        // Extract slug: first part of host, or X-Tenant-Slug header for local dev
        var slug = ExtractSlug(host, httpContext.Request.Headers);

        if (!string.IsNullOrEmpty(slug) && !IsPlatformHost(host))
        {
            // Lookup tenant by slug or by TenantDomain
            subdomainTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug && !t.IsDeleted);
            if (subdomainTenant == null)
            {
                // Try custom domain lookup
                var domainEntity = await db.TenantDomains.Include(d=>d.TenantId).AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Domain == host && !d.IsDeleted && d.IsVerified);
                // Simplified: if domain found, get tenant via navigation? We store tenant via TenantId
                // For demo, we try lookup tenant via domain table directly
                var tenantDomain = await db.TenantDomains.AsNoTracking().FirstOrDefaultAsync(d => d.Domain == host && !d.IsDeleted);
                if (tenantDomain != null)
                {
                    subdomainTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantDomain.TenantId && !t.IsDeleted);
                }
            }

            if (subdomainTenant != null)
            {
                subdomainTenantId = subdomainTenant.Id;
                // For anonymous, set as current tenant context (login branding)
                if (!httpContext.User.Identity?.IsAuthenticated ?? true)
                {
                    tenantContext.SetSubdomainTenant(subdomainTenantId, subdomainTenant);
                    httpContext.Items["Tenant"] = subdomainTenant;
                    httpContext.Items["SubdomainTenantId"] = subdomainTenantId;
                }
            }
            else
            {
                // Subdomain provided but tenant not found - for security, we still continue but log? For login page, show generic branding
                _logger.LogWarning("Subdomain {Slug} ({Host}) not resolved to tenant for request {Path}", slug, host, httpContext.Request.Path);
            }
        }

        // If authenticated, resolve from JWT claim tid (source of truth)
        if (httpContext.User.Identity?.IsAuthenticated ?? false)
        {
            var tidClaim = httpContext.User.FindFirst("tid")?.Value ?? httpContext.User.FindFirst("tenant_id")?.Value;
            var uidClaim = httpContext.User.FindFirst("uid")?.Value ?? httpContext.User.FindFirst("sub")?.Value;
            long? tokenTenantId = null;
            if (long.TryParse(tidClaim, out var tid) && tid != 0)
                tokenTenantId = tid;

            long.TryParse(uidClaim, out var actorUserId);

            // Platform admin: tid = 0 or null and role PLATFORM_SUPERADMIN
            var isPlatformRole = httpContext.User.IsInRole("PLATFORM_SUPERADMIN") || httpContext.User.HasClaim("roles","PLATFORM_SUPERADMIN") || httpContext.User.FindAll(ClaimTypes.Role).Any(r=>r.Value=="PLATFORM_SUPERADMIN");

            if (tokenTenantId.HasValue)
            {
                // Load tenant for context
                var tokenTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tokenTenantId.Value && !t.IsDeleted);
                if (tokenTenant == null)
                {
                    _logger.LogWarning("Token tenant {TokenTenantId} not found for user {UserId}", tokenTenantId, actorUserId);
                    httpContext.Response.StatusCode = 403;
                    await httpContext.Response.WriteAsJsonAsync(new { message = "Invalid tenant in token" });
                    return;
                }

                // 4. Return 403 and log security event if subdomain tenant and token tenant disagree
                if (subdomainTenantId.HasValue && tokenTenantId.Value != subdomainTenantId.Value)
                {
                    // Security event
                    _logger.LogCritical("SECURITY: Subdomain tenant {SubdomainTenantId} ({Slug}) != Token tenant {TokenTenantId} for user {UserId} IP {Ip} Path {Path}. Possible host header tampering or token replay across tenants.",
                        subdomainTenantId, slug, tokenTenantId, actorUserId, httpContext.Connection.RemoteIpAddress, httpContext.Request.Path);

                    // Write audit log for security event
                    try
                    {
                        db.AuditLogs.Add(new AuditLog
                        {
                            TenantId = tokenTenantId,
                            UserId = actorUserId,
                            EntityType = "Tenant",
                            EntityId = subdomainTenantId.Value,
                            Action = "security_violation",
                            OldValues = $"{{\"subdomainTenantId\":{subdomainTenantId},\"tokenTenantId\":{tokenTenantId},\"host\":\"{host}\"}}",
                            NewValues = $"{{\"path\":\"{httpContext.Request.Path}\"}}",
                            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                            UserAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault(),
                            Reason = "subdomain_token_mismatch"
                        });
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to write security audit for tenant mismatch");
                    }

                    httpContext.Response.StatusCode = 403;
                    await httpContext.Response.WriteAsJsonAsync(new { message = "Tenant mismatch: subdomain and token disagree", code = "TENANT_MISMATCH" });
                    return;
                }

                // Set resolved tenant from token (source of truth)
                tenantContext.SetResolvedTenant(tokenTenant.Id, tokenTenant, TenantResolutionSource.JwtToken, subdomainTenantId, tokenTenantId, actorUserId);
                httpContext.Items["Tenant"] = tokenTenant;
                httpContext.Items["TokenTenantId"] = tokenTenantId;
            }
            else if (isPlatformRole)
            {
                // Platform admin - no tenant, but actor is platform
                // They must use explicit no-tenant scope for genuinely tenant-less ops via service, not via this middleware
                // Here we leave TenantId null but set actor
                tenantContext.SetSubdomainTenant(subdomainTenantId, subdomainTenant); // keep subdomain for logging
                // Mark as platform: we set actor but no tenant, via internal method - use explicit? For now set via reflection of context state
                // We will not set explicit no-tenant here, just leave unresolved for platform routes that allow anonymous? Platform routes should use [RequiresPermission PLATFORM...] and explicit scope inside service
                // For platform admin accessing tenant-specific data, they must specify tenant via header or query? For V1 we require tid claim, so platform admin without tid cannot access tenant data - must be explicit
                _logger.LogInformation("Platform admin user {UserId} request {Path} subdomain {SubdomainTenantId} resolved", actorUserId, httpContext.Request.Path, subdomainTenantId);
            }
        }
        else
        {
            // Anonymous: if subdomain resolved, context already set via SetSubdomainTenant
            // For registration endpoint, slug may be in body not subdomain - that's ok, we still have context from subdomain for branding
        }

        // Store tenant context in HttpContext for downstream
        httpContext.Items["TenantContext"] = tenantContext;

        await _next(httpContext);
    }

    private static string? ExtractSlug(string host, IHeaderDictionary headers)
    {
        // Allow X-Tenant-Slug header override for local dev / Postman
        if (headers.TryGetValue("X-Tenant-Slug", out var headerSlug) && !string.IsNullOrEmpty(headerSlug))
            return headerSlug.ToString().ToLowerInvariant();

        // host = petra.learncloud.co.zw => slug petra
        // host = localhost:5000 => no slug
        // host = api.learncloud.co.zw => api is not tenant slug (reserved)
        var reserved = new HashSet<string> { "api", "www", "app", "admin", "platform", "learncloud" };

        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            var slug = parts[0];
            if (reserved.Contains(slug)) return null;
            return slug;
        }
        // For custom domain like portal.hillcrest.ac.zw we return full host for domain lookup, but slug extraction fails - we handle via TenantDomain lookup using full host
        if (parts.Length == 4 && host.EndsWith(".ac.zw")) // custom? Keep full host
        {
            return null; // will be resolved via TenantDomain table using full host
        }
        return null;
    }

    private static bool IsPlatformHost(string host)
    {
        return host.Equals("learncloud.co.zw") || host.Equals("www.learncloud.co.zw") || host.StartsWith("localhost") || host.StartsWith("127.0.0.1");
    }
}

// Extension for ordering correctly relative to authentication
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}

// Required ordering note for Program.cs:
// app.UseRouting();
// app.UseAuthentication(); // JWT bearer validates tid claim
// app.UseMiddleware<TenantResolutionMiddleware>(); // after auth, before authorization
// app.UseAuthorization();
// app.UseEndpoints...
}
