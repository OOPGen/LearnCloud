using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TenantIsolationTests
{
    private readonly LearnCloudApiFixture _api;

    public TenantIsolationTests(LearnCloudApiFixture api) => _api = api;

    [Fact]
    public async Task School_cannot_see_or_change_another_schools_subject_over_http()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        using var b = _api.ClientFor(_api.SchoolB);

        var create = await a.PostAsJsonAsync("/api/academic/subjects", new { Name = "Physics", Code = $"PHY{Random.Shared.Next(1000, 9999)}", Description = "", IsCore = true, Department = "Sciences" });
        await LearnCloudApiFixture.EnsureSuccessAsync(create, "create subject");
        var subjectId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var ownList = await a.GetFromJsonAsync<JsonElement>("/api/academic/subjects?pageSize=100");
        Assert.Contains(ownList.GetProperty("items").EnumerateArray(), s => s.GetProperty("id").GetInt64() == subjectId);

        var otherList = await b.GetFromJsonAsync<JsonElement>("/api/academic/subjects?pageSize=100");
        Assert.DoesNotContain(otherList.GetProperty("items").EnumerateArray(), s => s.GetProperty("id").GetInt64() == subjectId);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/academic/subjects/{subjectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/academic/subjects/{subjectId}", new { Name = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/academic/subjects/{subjectId}")).StatusCode);

        // A's subject is untouched by B's attempts.
        var stillThere = await a.GetFromJsonAsync<JsonElement>($"/api/academic/subjects/{subjectId}");
        Assert.Equal("Physics", stillThere.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Token_used_under_another_schools_slug_is_rejected()
    {
        using var aOnB = _api.ClientFor(_api.SchoolA, tenantSlugHeader: _api.SchoolB.Slug);

        var response = await aOnB.GetAsync("/api/academic/subjects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_to_tenant_data_is_rejected()
    {
        using var anonymous = _api.Factory.CreateClient();

        var response = await anonymous.GetAsync("/api/academic/subjects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The per-tenant model cache was removed in Phase 2: one compiled EF model now serves
    // every tenant, and the query filter must read the tenant of the context running the
    // query. This exercises both tenants through the same cached model in one process.
    [Fact]
    public async Task Query_filter_uses_the_tenant_of_the_current_context()
    {
        var code = $"ISO{Random.Shared.Next(1000, 9999)}";

        await using (var scopeA = _api.Factory.Services.CreateAsyncScope())
        {
            var tenantA = scopeA.ServiceProvider.GetRequiredService<ITenantContext>();
            using var _ = tenantA.BeginTenantScope(_api.SchoolA.TenantId);
            var db = scopeA.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            db.Set<Subject>().Add(new Subject { Name = "Isolation probe", Code = code });
            await db.SaveChangesAsync();
        }

        await using var scopeB = _api.Factory.Services.CreateAsyncScope();
        var tenantB = scopeB.ServiceProvider.GetRequiredService<ITenantContext>();
        using var __ = tenantB.BeginTenantScope(_api.SchoolB.TenantId);
        var dbB = scopeB.ServiceProvider.GetRequiredService<LearnCloudDbContext>();

        Assert.False(await dbB.Set<Subject>().AnyAsync(s => s.Code == code));

        var raw = await dbB.Set<Subject>().IgnoreQueryFilters().SingleAsync(s => s.Code == code);
        Assert.Equal(_api.SchoolA.TenantId, raw.TenantId);
    }

    [Fact]
    public async Task Saving_a_row_for_another_tenant_is_refused()
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolB.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();

        db.Set<Subject>().Add(new Subject { TenantId = _api.SchoolA.TenantId, Name = "Cross-tenant write", Code = "XT" });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("TenantId mismatch", error.Message);
    }

    [Fact]
    public async Task Deleting_keeps_the_row_as_soft_deleted()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var create = await a.PostAsJsonAsync("/api/academic/subjects", new { Name = "Art", Code = $"ART{Random.Shared.Next(1000, 9999)}", Description = "", IsCore = false, Department = "Arts" });
        await LearnCloudApiFixture.EnsureSuccessAsync(create, "create subject");
        var subjectId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        Assert.True((await a.DeleteAsync($"/api/academic/subjects/{subjectId}")).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/academic/subjects/{subjectId}")).StatusCode);

        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var row = await db.Set<Subject>().IgnoreQueryFilters().SingleAsync(s => s.Id == subjectId);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
    }
}
