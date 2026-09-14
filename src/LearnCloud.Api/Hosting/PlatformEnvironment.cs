using Npgsql;

namespace LearnCloud.Api.Hosting;

// Adapts hosting-platform conventions (Railway, and most PaaS hosts) to ASP.NET Core
// configuration, so the same image runs locally, in CI and on Railway unchanged.
public static class PlatformEnvironment
{
    /// <summary>
    /// Railway injects PORT and routes traffic to it. Bind there when it is set.
    /// </summary>
    public static void BindPlatformPort(this ConfigureWebHostBuilder webHost)
    {
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var number) && number is > 0 and < 65536)
            webHost.UseUrls($"http://0.0.0.0:{number}");
    }

    /// <summary>
    /// Railway's PostgreSQL service exposes DATABASE_URL as a postgresql:// URI, which
    /// Npgsql does not accept. When no ConnectionStrings:Default is configured, convert it.
    /// An explicit ConnectionStrings:Default always wins.
    /// </summary>
    public static void UseDatabaseUrlIfPresent(this ConfigurationManager configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default"))) return;

        var url = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(url)) return;

        configuration["ConnectionStrings:Default"] = ConvertDatabaseUrl(url);
    }

    public static string ConvertDatabaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
            throw new InvalidOperationException("DATABASE_URL must be a postgres:// or postgresql:// URI.");

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            // Railway's private network (*.railway.internal) is plain TCP; its public proxy
            // offers TLS. Prefer uses TLS when available without failing when it is not.
            SslMode = SslMode.Prefer
        };

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (query["sslmode"] is { } sslmode && Enum.TryParse<SslMode>(sslmode.Replace("-", ""), ignoreCase: true, out var mode))
            builder.SslMode = mode;

        return builder.ConnectionString;
    }
}
