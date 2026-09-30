using System.Security.Cryptography;
using System.Text;
using LearnCloud.OnlinePayments.Services.Gateways;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LearnCloud.OnlinePayments.Tests;

/// <summary>
/// The payment webhook is anonymous by necessity — the gateway calls it, not a signed-in
/// user — so everything rests on the signature. It used to accept an unsigned payload that
/// merely carried a reference and an amount, which meant anyone could report a payment.
/// </summary>
public class PayNowWebhookSecurityTests
{
    // Not a credential: a fixed fake key so the expected signatures below are reproducible.
    // The secret scan in CI matches any long string named "Key", so it is told to skip this.
    private const string Key = "integration-key-of-at-least-32-characters"; // gitleaks:allow

    private static PayNowGateway Gateway(string key = Key) =>
        new(Options.Create(new PayNowOptions
        {
            IntegrationId = "12345",
            IntegrationKey = key,
            MerchantId = "merchant",
        }), NullLogger<PayNowGateway>.Instance, new HttpClient());

    // The gateway's own rule: SHA512 over the values in key order, plus the integration key.
    private static string SignedBody(Dictionary<string, string> fields)
    {
        var input = string.Join("", fields.OrderBy(kv => kv.Key).Select(kv => kv.Value)) + Key;
        var hash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(input)));
        var all = fields.Concat(new[] { new KeyValuePair<string, string>("hash", hash) });
        return string.Join("&", all.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static Dictionary<string, string> PaidFields() => new()
    {
        ["reference"] = "LC-1-abc",
        ["paynowreference"] = "PN-1",
        ["amount"] = "250.00",
        ["status"] = "paid",
    };

    [Fact]
    public async Task A_correctly_signed_webhook_is_accepted()
    {
        var result = await Gateway().VerifyWebhookAsync(new VerifyWebhookRequest { Payload = SignedBody(PaidFields()) });

        Assert.True(result.IsValid);
        Assert.Equal("succeeded", result.Status);
        Assert.Equal(250.00m, result.Amount);
    }

    [Fact]
    public async Task An_unsigned_webhook_is_refused()
    {
        var fields = PaidFields();
        var body = string.Join("&", fields.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

        var result = await Gateway().VerifyWebhookAsync(new VerifyWebhookRequest { Payload = body });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_webhook_signed_with_the_wrong_key_is_refused()
    {
        // Signed correctly, but by someone who does not know our integration key.
        var body = SignedBody(PaidFields());

        var result = await Gateway("a-different-key-of-at-least-32-characters").VerifyWebhookAsync(new VerifyWebhookRequest { Payload = body });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Tampering_with_the_amount_invalidates_the_signature()
    {
        var body = SignedBody(PaidFields()).Replace("250.00", "9999.00");

        var result = await Gateway().VerifyWebhookAsync(new VerifyWebhookRequest { Payload = body });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void The_gateway_refuses_to_start_without_an_integration_key()
    {
        // Nothing can be verified without the key, so the gateway never exists without one
        // and the webhook has no unverified path to fall back to.
        Assert.Throws<InvalidOperationException>(() => Gateway(key: ""));
    }

    [Fact]
    public async Task Initiating_a_payment_refuses_rather_than_inventing_a_payment_link()
    {
        // The call to PayNow is not implemented; it used to return a fabricated paynow.co.zw
        // link and a made-up reference, which a parent would have followed to nothing.
        var result = await Gateway().InitiatePaymentAsync(new InitiatePaymentRequest
        {
            TenantId = 1,
            StudentId = 2,
            Amount = 100m,
            Currency = "USD",
            Method = "card",
            ClientReference = "LC-1-abc",
        });

        Assert.False(result.Success);
        Assert.Null(result.PaymentUrl);
    }
}
