using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;
using Xunit.Abstractions;

namespace LearnCloud.IntegrationTests;

// Calls every GET endpoint that needs no route values as a school administrator. Most
// modules had never been run: a query EF Core cannot translate, a missing registration or
// an unmapped column only shows up at runtime, as a 500. This catches that for every module.
[Collection(ApiCollection.Name)]
public sealed class EndpointSweepTests
{
    private readonly LearnCloudApiFixture _api;
    private readonly ITestOutputHelper _output;

    public EndpointSweepTests(LearnCloudApiFixture api, ITestOutputHelper output)
    {
        _api = api;
        _output = output;
    }

    [Fact]
    public async Task No_parameterless_GET_endpoint_fails_with_a_server_error()
    {
        var endpoints = _api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
            .Where(e => e.RoutePattern.Parameters.All(p => p.IsOptional || p.Default is not null))
            .Select(e => "/" + string.Join("/", e.RoutePattern.PathSegments.Select(s => string.Concat(s.Parts.OfType<RoutePatternLiteralPart>().Select(p => p.Content)))))
            .Where(path => path.StartsWith("/api/", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        using var client = _api.ClientFor(_api.SchoolA);
        var failures = new List<string>();
        foreach (var path in endpoints)
        {
            var response = await client.GetAsync(path);
            var status = (int)response.StatusCode;
            _output.WriteLine($"{status} {path}");
            if (status >= 500) failures.Add($"{status} {path}: {Truncate(await response.Content.ReadAsStringAsync())}");
        }

        _output.WriteLine($"{endpoints.Count} endpoints called");
        Assert.True(endpoints.Count > 50, $"Only {endpoints.Count} endpoints found; the sweep is not seeing the modules.");
        Assert.True(failures.Count == 0, $"{failures.Count} endpoints failed:\n{string.Join("\n", failures)}");
    }

    // Fees endpoints named policies that were never registered, and failed on every call.
    [Fact]
    public async Task Every_authorization_policy_named_by_an_endpoint_is_registered()
    {
        var provider = _api.Factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var names = _api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .SelectMany(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>())
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        var missing = new List<string>();
        foreach (var name in names)
            if (await provider.GetPolicyAsync(name!) is null) missing.Add(name!);

        Assert.NotEmpty(names);
        Assert.Empty(missing);
    }

    // Endpoints that take ids, called with ids that do not exist: expect 404 (or a 4xx),
    // never a 500 from a query that cannot run.
    [Fact]
    public async Task GET_endpoints_with_unknown_ids_do_not_fail_with_a_server_error()
    {
        var endpoints = _api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
            .Where(e => e.RoutePattern.Parameters.Any(p => !p.IsOptional && p.Default is null))
            .Select(e => Fill(e.RoutePattern))
            .Where(path => path is not null && path.StartsWith("/api/", StringComparison.Ordinal))
            .Select(path => path!)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        using var client = _api.ClientFor(_api.SchoolA);
        var failures = new List<string>();
        foreach (var path in endpoints)
        {
            var response = await client.GetAsync(path);
            var status = (int)response.StatusCode;
            _output.WriteLine($"{status} {path}");
            if (status >= 500) failures.Add($"{status} {path}: {Truncate(await response.Content.ReadAsStringAsync())}");
        }

        _output.WriteLine($"{endpoints.Count} endpoints called");
        Assert.True(failures.Count == 0, $"{failures.Count} endpoints failed:\n{string.Join("\n", failures)}");
    }

    // Replaces route parameters with values that exist nowhere: 987654321 for numbers, a
    // fixed date, a zero GUID, or "unknown". Returns null for catch-all parameters.
    private static string? Fill(RoutePattern pattern)
    {
        var parts = new List<string>();
        foreach (var segment in pattern.PathSegments)
        {
            var text = "";
            foreach (var part in segment.Parts)
            {
                switch (part)
                {
                    case RoutePatternLiteralPart literal: text += literal.Content; break;
                    case RoutePatternSeparatorPart separator: text += separator.Content; break;
                    case RoutePatternParameterPart parameter:
                        if (parameter.IsCatchAll) return null;
                        var constraints = parameter.ParameterPolicies.Select(p => p.Content ?? "").ToList();
                        text += constraints.Any(c => c is "long" or "int") || parameter.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || parameter.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ? "987654321"
                            : constraints.Contains("guid") ? Guid.Empty.ToString()
                            : constraints.Contains("datetime") ? "2026-03-02"
                            : "unknown";
                        break;
                }
            }
            parts.Add(text);
        }
        return "/" + string.Join("/", parts);
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}
