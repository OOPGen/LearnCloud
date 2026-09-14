using LearnCloud.MultiTenancy.Caching;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Interceptors;
using LearnCloud.MultiTenancy.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LearnCloud.MultiTenancy.Extensions;

public static class MultiTenancyExtensions
{
    /// <summary>
    /// Registers tenant resolution state and the single platform database context.
    /// </summary>
    /// <param name="migrationsAssembly">Assembly that holds EF Core migrations (the API host).</param>
    public static IServiceCollection AddLearnCloudMultiTenancy(this IServiceCollection services, IConfiguration config, string migrationsAssembly)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<INoTenantOperation, NoTenantOperation>();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ITenantSettingsCache, TenantSettingsCache>();

        // No per-tenant IModelCacheKeyFactory: the tenant filter reads the current context
        // instance on every query, so one compiled model is shared by all tenants.
        services.AddDbContext<LearnCloudDbContext>(opt =>
        {
            var conn = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured. For local development run scripts/dev-setup.ps1; in production set ConnectionStrings__Default.");
            if (conn.Contains("Password=root", StringComparison.OrdinalIgnoreCase) || conn.Contains("Password=learncloud;", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SECURITY: insecure default database password detected; use a generated password from configuration.");

            opt.UseNpgsql(conn, npgsql =>
                {
                    npgsql.MigrationsAssembly(migrationsAssembly);
                    npgsql.EnableRetryOnFailure(3);
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }

    // Required order in Program.cs:
    //   UseRouting -> UseRateLimiter -> UseAuthentication -> TenantResolutionMiddleware -> UseAuthorization -> MapControllers
}
