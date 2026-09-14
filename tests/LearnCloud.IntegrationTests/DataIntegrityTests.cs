using LearnCloud.Auth.Entities;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DataIntegrityTests
{
    private readonly LearnCloudApiFixture _api;

    public DataIntegrityTests(LearnCloudApiFixture api) => _api = api;

    // Scheduled jobs (dunning, communication rules) depend on this: the explicit
    // no-tenant flag must be read per query, like the tenant id.
    [Fact]
    public async Task Privileged_no_tenant_scope_sees_every_tenant()
    {
        var code = $"NTS{Random.Shared.Next(1000, 9999)}";
        await AddSubjectAsync(_api.SchoolA.TenantId, code);
        await AddSubjectAsync(_api.SchoolB.TenantId, code);

        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var noTenant = scope.ServiceProvider.GetRequiredService<INoTenantOperation>();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();

        using (noTenant.BeginScope("Integration test cross-tenant read", actorUserId: 0, actorRole: PrivilegedRoles.SystemJob))
        {
            var tenants = await db.Set<Subject>().Where(s => s.Code == code).Select(s => s.TenantId).ToListAsync();
            Assert.Contains(_api.SchoolA.TenantId, tenants);
            Assert.Contains(_api.SchoolB.TenantId, tenants);
        }

        // Scope ended: with no tenant resolved the filter returns nothing.
        Assert.False(await db.Set<Subject>().AnyAsync(s => s.Code == code));
    }

    [Fact]
    public async Task No_tenant_scope_is_refused_for_an_ordinary_role()
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var noTenant = scope.ServiceProvider.GetRequiredService<INoTenantOperation>();

        Assert.Throws<UnauthorizedAccessException>(() =>
            noTenant.BeginScope("Teacher attempting cross-tenant read", actorUserId: 1, actorRole: "TEACHER"));
    }

    // Deletes are soft, so unique indexes must ignore deleted rows or a deleted role could
    // never be recreated.
    [Fact]
    public async Task Soft_deleted_role_code_can_be_used_again()
    {
        var code = $"ROLE_{Random.Shared.Next(1000, 9999)}";

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var role = new Role { TenantId = _api.SchoolA.TenantId, Code = code, Name = "Temporary" };
            db.Set<Role>().Add(role);
            await db.SaveChangesAsync();
            db.Set<Role>().Remove(role);
            await db.SaveChangesAsync();
        }

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            db.Set<Role>().Add(new Role { TenantId = _api.SchoolA.TenantId, Code = code, Name = "Recreated" });
            await db.SaveChangesAsync();

            var rows = await db.Set<Role>().IgnoreQueryFilters().Where(r => r.Code == code).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Single(rows, r => !r.IsDeleted);
        }
    }

    // Platform users have no tenant. PostgreSQL treats NULLs as distinct in unique
    // indexes by default, which allowed duplicate platform accounts.
    [Fact]
    public async Task Two_platform_accounts_cannot_share_an_email()
    {
        var email = $"ops{Random.Shared.Next(100000, 999999)}@platform.test";

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            db.Set<User>().Add(NewPlatformUser(email));
            await db.SaveChangesAsync();
        }

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            db.Set<User>().Add(NewPlatformUser(email));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private static User NewPlatformUser(string email) => new()
    {
        TenantId = null,
        Email = email,
        DisplayName = "Platform Ops",
        PasswordHash = "not-a-real-hash",
        SecurityStamp = Guid.NewGuid().ToString(),
        Status = "active"
    };

    private async Task AddSubjectAsync(long tenantId, string code)
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        db.Set<Subject>().Add(new Subject { Name = "Probe", Code = code });
        await db.SaveChangesAsync();
    }
}
