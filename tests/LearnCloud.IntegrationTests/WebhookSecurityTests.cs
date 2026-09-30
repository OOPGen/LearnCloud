using System.Net;
using System.Net.Http.Json;
using LearnCloud.Communication.Controllers;
using LearnCloud.Communication.DTOs;
using Xunit;

namespace LearnCloud.IntegrationTests;

/// <summary>
/// Phase 6: the endpoints an outside service calls. They cannot carry a user's token, so
/// each one proves itself with a shared secret — and until that secret is configured, the
/// endpoint is closed rather than open.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WebhookSecurityTests
{
    private readonly LearnCloudApiFixture _api;

    public WebhookSecurityTests(LearnCloudApiFixture api) => _api = api;

    private static WebhookInboundSmsRequest Message() =>
        new("+263771234567", "+263780000000", "YES", "provider-ref-1", "test-provider");

    private HttpRequestMessage Inbound(string? secret, string? tenantId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/communication/inbound-sms/webhook")
        {
            Content = JsonContent.Create(Message()),
        };
        if (secret is not null) request.Headers.Add(CommunicationController.WebhookSecretHeader, secret);
        if (tenantId is not null) request.Headers.Add("X-Tenant-Id", tenantId);
        return request;
    }

    [Fact]
    public async Task Inbound_sms_without_the_shared_secret_is_refused()
    {
        using var anonymous = _api.Factory.CreateClient();

        // This used to be accepted, and the caller named the school it wanted to write into.
        var response = await anonymous.SendAsync(Inbound(secret: null, tenantId: _api.SchoolA.TenantId.ToString()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_sms_with_the_wrong_secret_is_refused()
    {
        using var anonymous = _api.Factory.CreateClient();

        var response = await anonymous.SendAsync(Inbound("not-the-secret", _api.SchoolA.TenantId.ToString()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_sms_must_name_a_school_that_exists()
    {
        using var anonymous = _api.Factory.CreateClient();

        // No tenant at all: there is no "first tenant" fallback any more.
        var missing = await anonymous.SendAsync(Inbound(LearnCloudApiFixture.InboundSmsSecret, tenantId: null));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var unknown = await anonymous.SendAsync(Inbound(LearnCloudApiFixture.InboundSmsSecret, "987654321"));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Inbound_sms_from_the_provider_is_accepted()
    {
        using var anonymous = _api.Factory.CreateClient();

        var response = await anonymous.SendAsync(Inbound(LearnCloudApiFixture.InboundSmsSecret, _api.SchoolA.TenantId.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_payment_webhook_refuses_an_unsigned_report_of_a_payment()
    {
        using var anonymous = _api.Factory.CreateClient();

        // The shape that used to be accepted: a reference and an amount, no signature.
        var response = await anonymous.PostAsync("/api/webhooks/payments/paynow",
            new StringContent("reference=LC-1-abc&amount=5000.00&status=paid", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable,
            $"an unsigned payment webhook must not be accepted, got {(int)response.StatusCode}");
    }
}
