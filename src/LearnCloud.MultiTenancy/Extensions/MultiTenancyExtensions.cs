using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Interceptors;
using LearnCloud.MultiTenancy.Middleware;
using LearnCloud.MultiTenancy.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LearnCloud.MultiTenancy.Extensions;

public static class MultiTenancyExtensions
{
    public static IServiceCollection AddLearnCloudMultiTenancy(this IServiceCollection services, IConfiguration config)
    {
        // 2. ITenantEntity + BaseEntity already defined

        // 3. ITenantContext singleton? Must be scoped per request (AsyncLocal)
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<INoTenantOperation, NoTenantOperation>();
        services.AddHttpContextAccessor();
        // SECURITY C5: Model cache key includes tenantId to prevent cache poisoning
        services.AddSingleton<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, Context.TenantModelCacheKeyFactory>();

        // 5. DbContext with global query filter via reflection + automatic TenantId assignment + audit
        // SECURITY C1 FIX: No fallback connection string, fail fast if not configured
        services.AddDbContext<LearnCloudDbContext>((sp, opt) =>
        {
            var conn = config.GetConnectionString("Default") ?? throw new InvalidOperationException("SECURITY: ConnectionStrings__Default must be set via env - no default root password allowed. Set MYSQL_PASSWORD via .env.production outside repo");
            if (conn.Contains("Password=root", StringComparison.OrdinalIgnoreCase) || conn.Contains("Password=learncloud", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SECURITY: Insecure default DB password detected - must use strong password from env");
            opt.UseNpgsql(conn, npgsql => { npgsql.EnableRetryOnFailure(3); npgsql.UseQuerySplittingBehavior(Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery); });
        });

        return services;
    }

    // Ordering note: Must be called in Program.cs in this exact order:
    // app.UseRouting();
    // app.UseRateLimiter();
    // app.UseAuthentication(); // validates JWT, populates User with tid claim
    // app.UseMiddleware<TenantResolutionMiddleware>(); // after auth, before authz - checks subdomain vs token 403 + logs security event
    // app.UseAuthorization();
    // app.MapControllers();

    public static IServiceCollection AddTenantResolution(this IServiceCollection services)
    {
        // No extra, middleware registered via UseMiddleware
        return services;
    }
}
