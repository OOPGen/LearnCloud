using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LearnCloud.Api.Data;

// Used only by `dotnet ef` (adding migrations, generating SQL scripts). It builds the
// context without starting the web host, so tooling does not need a JWT secret or any
// other runtime configuration. Creating a migration never connects to the database;
// commands that do (database update) read ConnectionStrings__Default from the environment.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LearnCloudDbContext>
{
    public LearnCloudDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=learncloud_design;Username=design;Password=design-time-only";

        var options = new DbContextOptionsBuilder<LearnCloudDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;

        var tenantContext = new TenantContext();
        return new LearnCloudDbContext(options, tenantContext, new AuditInterceptor(tenantContext));
    }
}
