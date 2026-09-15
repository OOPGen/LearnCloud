using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LearnCloud.IntegrationTests;

// Runs the real API in memory against a throwaway PostgreSQL container with the real EF
// Core migrations applied. Requires Docker (available locally and on GitHub runners).
//
// Two schools are registered once per test run: registration is rate limited to three
// per hour per client, so each test reuses them instead of registering its own.
public sealed class LearnCloudApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public TestSchool SchoolA { get; private set; } = null!;
    public TestSchool SchoolB { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            ["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["Jobs:Enabled"] = "false",
            // Every test calls the API as one of two admins; the production limit of 60 a
            // minute per user would throttle the suite.
            ["RateLimiting:ApiGeneralPerMinute"] = "5000"
        };

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // "Testing" so developer user-secrets (loaded only in Development) are not used.
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
        });

        await using (var scope = Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Database.MigrateAsync();

        var run = Guid.NewGuid().ToString("N")[..8];
        SchoolA = await RegisterAndLoginAsync($"ita{run}");
        SchoolB = await RegisterAndLoginAsync($"itb{run}");
    }

    public HttpClient ClientFor(TestSchool school, string? tenantSlugHeader = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", school.AccessToken);
        if (tenantSlugHeader is not null) client.DefaultRequestHeaders.Add("X-Tenant-Slug", tenantSlugHeader);
        return client;
    }

    private async Task<TestSchool> RegisterAndLoginAsync(string slug)
    {
        var client = Factory.CreateClient();
        var email = $"admin@{slug}.test";
        const string password = "Str0ng!Passw0rd";

        var register = await client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            SchoolName = $"Integration {slug}", Slug = slug, City = "Bulawayo", ContactEmail = email,
            ContactPhone = "+263771000000", LearnerCountBand = "150-300", AdminFullName = "Test Admin",
            AdminEmail = email, AdminPhone = "+263771000000", Password = password, ConfirmPassword = password
        });
        await EnsureSuccessAsync(register, $"register {slug}");
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password, TenantSlug = slug, Device = "integration-test" });
        await EnsureSuccessAsync(login, $"login {slug}");
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        return new TestSchool(slug, registered.GetProperty("tenantId").GetInt64(), token);
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, string step)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{step} failed: HTTP {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

public sealed record TestSchool(string Slug, long TenantId, string AccessToken);

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<LearnCloudApiFixture>
{
    public const string Name = "LearnCloud API";
}
