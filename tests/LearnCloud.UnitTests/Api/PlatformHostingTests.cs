using System.Net;
using LearnCloud.Api.Hosting;
using LearnCloud.Api.Middleware;
using Npgsql;
using Xunit;

namespace LearnCloud.Api.Tests;

public class DatabaseUrlTests
{
    [Fact]
    public void Railway_style_url_becomes_an_npgsql_connection_string()
    {
        var cs = new NpgsqlConnectionStringBuilder(
            PlatformEnvironment.ConvertDatabaseUrl("postgresql://postgres:s3cr%40t%2Fpw@postgres.railway.internal:5432/railway"));

        Assert.Equal("postgres.railway.internal", cs.Host);
        Assert.Equal(5432, cs.Port);
        Assert.Equal("railway", cs.Database);
        Assert.Equal("postgres", cs.Username);
        Assert.Equal("s3cr@t/pw", cs.Password); // percent-encoded characters are decoded
        Assert.Equal(SslMode.Prefer, cs.SslMode);
    }

    [Fact]
    public void Missing_port_defaults_to_5432_and_sslmode_query_is_honoured()
    {
        var cs = new NpgsqlConnectionStringBuilder(
            PlatformEnvironment.ConvertDatabaseUrl("postgres://user:pw@db.example.com/app?sslmode=require"));

        Assert.Equal(5432, cs.Port);
        Assert.Equal(SslMode.Require, cs.SslMode);
    }

    [Fact]
    public void Non_postgres_url_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => PlatformEnvironment.ConvertDatabaseUrl("mysql://root:root@localhost/app"));
    }
}

public class TrustedProxyClientIpMiddlewareTests
{
    private static async Task<HttpContext> RunAsync(string? configuredSecret, IDictionary<string, string> headers)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Proxy:SharedSecret"] = configuredSecret })
            .Build();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5"); // the proxy's address
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;

        var middleware = new TrustedProxyClientIpMiddleware(_ => Task.CompletedTask, config);
        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task Client_ip_is_used_when_the_secret_matches()
    {
        var context = await RunAsync("proxy-secret-value", new Dictionary<string, string>
        {
            [TrustedProxyClientIpMiddleware.SecretHeader] = "proxy-secret-value",
            [TrustedProxyClientIpMiddleware.ClientIpHeader] = "196.4.10.20",
        });

        Assert.Equal(IPAddress.Parse("196.4.10.20"), context.Connection.RemoteIpAddress);
        Assert.False(context.Request.Headers.ContainsKey(TrustedProxyClientIpMiddleware.SecretHeader));
    }

    [Theory]
    [InlineData("proxy-secret-value", "wrong-secret")]
    [InlineData(null, "anything")]
    public async Task Client_ip_is_ignored_without_the_right_secret(string? configured, string presented)
    {
        var context = await RunAsync(configured, new Dictionary<string, string>
        {
            [TrustedProxyClientIpMiddleware.SecretHeader] = presented,
            [TrustedProxyClientIpMiddleware.ClientIpHeader] = "196.4.10.20",
        });

        Assert.Equal(IPAddress.Parse("10.0.0.5"), context.Connection.RemoteIpAddress);
        Assert.False(context.Request.Headers.ContainsKey(TrustedProxyClientIpMiddleware.SecretHeader));
    }
}
