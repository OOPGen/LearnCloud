using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnCloud.Api.Hosting;
using LearnCloud.Auth.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Security;
using LearnCloud.PlatformAdmin.Entities;
using LearnCloud.PlatformAdmin.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.IntegrationTests;

/// <summary>
/// Phase 6. The platform console was unusable and unguarded at once: nothing could create a
/// PLATFORM_SUPERADMIN, every platform request failed with 500 because the second-factor
/// middleware read a session that was never configured, and the check it performed accepted
/// the code "123456". These tests cover the account, the gate and the way through it.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformAdminBootstrapTests
{
    private readonly LearnCloudApiFixture _api;

    public PlatformAdminBootstrapTests(LearnCloudApiFixture api) => _api = api;

    private async Task<(string Email, string Secret)> CreatePlatformAdminAsync(string password)
    {
        var email = $"plat{Guid.NewGuid():N}"[..16] + "@learncloud.test";
        var secret = Totp.NewSecret();

        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var noTenant = scope.ServiceProvider.GetRequiredService<INoTenantOperation>();
        using var _ = noTenant.BeginScope("Test platform administrator", 0, PrivilegedRoles.SystemJob);

        var role = await db.Set<Role>().FirstOrDefaultAsync(r => r.TenantId == null && r.Code == PrivilegedRoles.PlatformSuperAdmin);
        if (role is null)
        {
            role = new Role { TenantId = null, Code = PrivilegedRoles.PlatformSuperAdmin, Name = "Platform Superadmin", IsSystem = true };
            db.Set<Role>().Add(role);
            await db.SaveChangesAsync();
        }

        var admin = new User
        {
            TenantId = null, Email = email, DisplayName = "Platform Administrator", Status = "active",
            EmailVerified = true, SecurityStamp = Guid.NewGuid().ToString(), TokenVersion = 1,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Set<User>().Add(admin);
        await db.SaveChangesAsync();

        db.Set<UserRole>().Add(new UserRole { TenantId = null, UserId = admin.Id, RoleId = role.Id });
        db.Set<PlatformSecondFactor>().Add(new PlatformSecondFactor { UserId = admin.Id, Secret = secret });
        await db.SaveChangesAsync();
        return (email, secret);
    }

    private static string CurrentCode(string secret) =>
        Totp.Generate(Totp.FromBase32(secret), DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

    private static async Task<HttpClient> SignInAsync(LearnCloudApiFixture api, string email, string password)
    {
        var client = api.Factory.CreateClient();
        // A platform administrator belongs to no school, so there is no school code.
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password, tenantSlug = (string?)null });
        Assert.True(login.IsSuccessStatusCode, $"{(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task The_console_needs_a_code_from_the_authenticator_and_then_opens()
    {
        const string password = "Pl4tform!Admin-Passw0rd";
        var (email, secret) = await CreatePlatformAdminAsync(password);
        using var client = await SignInAsync(_api, email, password);

        // Signed in, but the console is shut until a code is given. This used to answer 500.
        var beforeStepUp = await client.GetAsync("/api/platform/enquiries");
        Assert.Equal(HttpStatusCode.Forbidden, beforeStepUp.StatusCode);
        Assert.Contains("second_factor_required", await beforeStepUp.Content.ReadAsStringAsync());

        var wrong = await client.PostAsJsonAsync("/api/platform/second-factor/verify", new { code = "123456", recoveryCode = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var code = CurrentCode(secret);
        var verify = await client.PostAsJsonAsync("/api/platform/second-factor/verify", new { code, recoveryCode = (string?)null });
        Assert.True(verify.IsSuccessStatusCode, $"{(int)verify.StatusCode}: {await verify.Content.ReadAsStringAsync()}");

        var afterStepUp = await client.GetAsync("/api/platform/enquiries");
        Assert.True(afterStepUp.IsSuccessStatusCode, $"{(int)afterStepUp.StatusCode}: {await afterStepUp.Content.ReadAsStringAsync()}");

        // The same code cannot open the console twice.
        var replay = await client.PostAsJsonAsync("/api/platform/second-factor/verify", new { code, recoveryCode = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // And it can be locked again.
        Assert.True((await client.PostAsync("/api/platform/second-factor/end", null)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/platform/enquiries")).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_alone_no_longer_opens_the_console()
    {
        const string password = "Pl4tform!Admin-Passw0rd";
        var (email, secret) = await CreatePlatformAdminAsync(password);
        using var client = await SignInAsync(_api, email, password);

        // Any non-empty recovery code used to be accepted on its own, without checking it
        // against anything. A wrong code stays wrong however it is dressed up.
        var valid = CurrentCode(secret);
        var wrong = (valid[0] == '0' ? '1' : '0') + valid[1..];
        var response = await client.PostAsJsonAsync("/api/platform/second-factor/verify", new { code = wrong, recoveryCode = "anything-at-all" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/platform/enquiries")).StatusCode);
    }

    [Fact]
    public async Task A_school_administrator_still_cannot_reach_the_console()
    {
        using var school = _api.ClientFor(_api.SchoolA);

        Assert.Equal(HttpStatusCode.Forbidden, (await school.GetAsync("/api/platform/enquiries")).StatusCode);
    }

    [Fact]
    public void The_bootstrap_command_is_recognised_only_when_asked_for()
    {
        Assert.True(PlatformAdminBootstrap.IsRequested(new[] { "--create-platform-admin", "admin@x.test" }));
        Assert.False(PlatformAdminBootstrap.IsRequested(new[] { "--migrate" }));
    }
}
