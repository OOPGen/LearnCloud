using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LearnCloud.OnlinePayments.Services.Gateways;

// One concrete implementation, chosen per tenant by configuration, supporting card, bank transfer and mobile money where available
// PayNow is Zimbabwe-friendly: card, bank transfer, EcoCash, OneMoney

public class PayNowOptions
{
    // SECURITY C1 FIX: No demo defaults - all must be set via env, fail fast if missing
    public string IntegrationId { get; set; } = null!; // must be set via PayNow__IntegrationId env
    public string IntegrationKey { get; set; } = null!; // must be set via PayNow__IntegrationKey env, 32+ chars
    public string MerchantId { get; set; } = null!;
    public string ResultUrl { get; set; } = "https://learncloud.co.zw/api/webhooks/payments/paynow";
    public string ReturnUrl { get; set; } = "https://learncloud.co.zw/parent/payments/return";
    public decimal FeePercentage { get; set; } = 2.5m;
    public decimal FeeFixed { get; set; } = 0.10m;
    public string Currency { get; set; } = "USD";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IntegrationId))
            throw new InvalidOperationException("SECURITY: PayNow__IntegrationId must be set via env - no demo default allowed");
        if (string.IsNullOrWhiteSpace(IntegrationKey))
            throw new InvalidOperationException("SECURITY: PayNow__IntegrationKey must be set via env - no demo default allowed");
        if (IntegrationKey.Length < 32)
            throw new InvalidOperationException($"SECURITY: PayNow IntegrationKey must be >=32 chars, current {IntegrationKey.Length}");
        if (IntegrationId.Contains("demo") || IntegrationKey.Contains("demo"))
            throw new InvalidOperationException("SECURITY: PayNow demo keys detected - must use real production keys from PayNow dashboard");
    }
}

public class PayNowGateway : IPaymentGateway
{
    public string GatewayName => "PayNow";
    private readonly PayNowOptions _options;
    private readonly ILogger<PayNowGateway> _logger;
    private readonly HttpClient _http;

    public PayNowGateway(IOptions<PayNowOptions> options, ILogger<PayNowGateway> logger, HttpClient http)
    {
        _options = options.Value;
        // SECURITY: Validate no demo keys
        _options.Validate();
        _logger = logger;
        _http = http;
        // SECURITY: Never log IntegrationKey
        if (_options.IntegrationKey.Length < 32)
            throw new InvalidOperationException("PayNow IntegrationKey too short");
    }

    public bool SupportsMethod(string method)
    {
        var supported = new[] { "card", "bank_transfer", "mobile_money", "ecocash", "onemoney", "innbucks" };
        return supported.Contains(method.ToLower());
    }

    public async Task<decimal> CalculateFeeAsync(decimal amount, string method, CancellationToken ct = default)
    {
        // PayNow fee: 2.5% + $0.10 for card, 2% for mobile money, etc. Simplified
        var percentage = method.ToLower() switch
        {
            "card" => 2.5m,
            "bank_transfer" => 1.5m,
            _ => 2.0m // mobile money
        };
        var fee = Math.Round(amount * percentage / 100m + _options.FeeFixed, 2, MidpointRounding.AwayFromZero);
        return fee;
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default)
    {
        // In real PayNow, you would POST to https://www.paynow.co.zw/interface/initiatetransaction
        // with fields: id, reference, amount, additionalinfo, returnurl, resulturl, authemail, status
        // Here we mock, but structure preserves signature generation

        if (!SupportsMethod(request.Method))
        {
            return new InitiatePaymentResult { Success = false, FailureReason = $"Method {request.Method} not supported by {GatewayName}. Supported: card, bank_transfer, mobile_money" };
        }

        // Build PayNow payload
        var payload = new Dictionary<string, string>
        {
            ["id"] = _options.IntegrationId,
            ["reference"] = request.ClientReference,
            ["amount"] = request.Amount.ToString("F2"),
            ["additionalinfo"] = request.Description ?? $"Payment for student {request.StudentId} tenant {request.TenantId}",
            ["returnurl"] = request.ReturnUrl ?? _options.ReturnUrl,
            ["resulturl"] = _options.ResultUrl,
            ["authemail"] = request.CustomerInfo?.TryGetValue("email", out var email) == true ? email : "parent@example.com",
            ["status"] = "Message"
        };

        // Generate hash for PayNow: hash = SHA512(id+reference+amount+additionalinfo+returnurl+resulturl+authemail+status+integrationKey)
        var hashInput = string.Join("", payload.Values) + _options.IntegrationKey;
        var hash = GenerateSha512(hashInput);
        payload["hash"] = hash;

        // The call to PayNow is not written yet: this signed the payload and then invented a
        // reference and a paynow.co.zw link that leads nowhere. Saying so is better than
        // handing a parent a broken payment page and recording an initiation for it.
        await Task.CompletedTask;
        _logger.LogWarning("Online payment initiation refused: the PayNow integration is not implemented ({ClientRef})", request.ClientReference);

        return new InitiatePaymentResult
        {
            Success = false,
            FailureReason = "Online payment is not available yet. Please pay by the methods on your invoice; the school can record it.",
        };
    }

    public async Task<VerifyWebhookResult> VerifyWebhookAsync(VerifyWebhookRequest request, CancellationToken ct = default)
    {
        // Treat every webhook as hostile until verified - signature-verified, safe against replay and out-of-order
        // PayNow resulturl POST includes fields: reference, paynowreference, amount, status, pollurl, hash

        // Without the integration key nothing can be verified, so nothing is accepted. This
        // endpoint is anonymous by necessity - the gateway calls it - so an unverified payload
        // must never be treated as a payment.
        if (string.IsNullOrWhiteSpace(_options.IntegrationKey))
        {
            _logger.LogError("Payment webhook received but Payments:PayNow:IntegrationKey is not set, so it cannot be verified");
            return new VerifyWebhookResult { IsValid = false, FailureReason = "Gateway is not configured" };
        }

        try
        {
            // PayNow posts form-encoded fields: reference, paynowreference, amount, status, pollurl, hash
            var parsed = ParseFormOrJson(request.Payload);

            // hash = SHA512(values in key order, excluding hash, + integration key), which only
            // the gateway can produce. An HMAC of the raw body in the signature header is also
            // accepted, for gateways that sign that way.
            var receivedHash = parsed.TryGetValue("hash", out var h) ? h : null;
            var expectedHashInput = string.Join("", parsed.Where(kv => kv.Key != "hash").OrderBy(kv => kv.Key).Select(kv => kv.Value)) + _options.IntegrationKey;
            var expectedHash = GenerateSha512(expectedHashInput);

            var hashMatches = !string.IsNullOrEmpty(receivedHash)
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(receivedHash.ToLowerInvariant()), Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant()));
            var signatureMatches = !string.IsNullOrEmpty(request.Signature)
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.Signature.ToLowerInvariant()), Encoding.UTF8.GetBytes(GenerateHmacSha256(request.Payload, _options.IntegrationKey).ToLowerInvariant()));

            if (!hashMatches && !signatureMatches)
            {
                // Never say which part failed: that would help someone guess their way in.
                _logger.LogWarning("Payment webhook rejected: neither the hash nor the signature matched");
                return new VerifyWebhookResult { IsValid = false, FailureReason = "Invalid signature - rejected" };
            }

            // Extract fields
            var gatewayTxId = parsed.TryGetValue("paynowreference", out var pr) ? pr : parsed.TryGetValue("gateway_reference", out var gr) ? gr : Guid.NewGuid().ToString();
            var clientRef = parsed.TryGetValue("reference", out var cref) ? cref : null;
            var amountStr = parsed.TryGetValue("amount", out var amt) ? amt : "0";
            var amount = decimal.TryParse(amountStr, out var a) ? a : 0m;
            var status = parsed.TryGetValue("status", out var st) ? st.ToLower() : "paid"; // PayNow status Paid, Awaiting, etc.
            var method = parsed.TryGetValue("method", out var m) ? m : "card";

            // Map PayNow status to our status
            var mappedStatus = status switch
            {
                "paid" => "succeeded",
                "awaiting" => "pending",
                "failed" => "failed",
                "cancelled" => "cancelled",
                _ => status
            };

            // Replay detection handled by caller via GatewayTransaction table unique GatewayTransactionId
            // Out-of-order safe: we store transaction even if initiation record not yet committed (see below)

            return new VerifyWebhookResult
            {
                IsValid = true,
                GatewayTransactionId = gatewayTxId,
                Amount = amount,
                Currency = "USD",
                Status = mappedStatus,
                ClientReference = clientRef,
                Method = method,
                ProviderReference = gatewayTxId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook verification exception - hostile?");
            return new VerifyWebhookResult { IsValid = false, FailureReason = $"Exception verifying webhook: {ex.Message}" };
        }
    }

    private static string GenerateSha512(string input)
    {
        using var sha = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    private static string GenerateHmacSha256(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLower();
    }

    private static Dictionary<string, string> ParseFormOrJson(string payload)
    {
        var result = new Dictionary<string, string>();
        try
        {
            // Try JSON first
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload);
            if (json != null)
            {
                foreach (var kv in json)
                {
                    result[kv.Key] = kv.Value.ToString();
                }
                return result;
            }
        }
        catch { }

        // Try form-url-encoded: key1=val1&key2=val2
        var pairs = payload.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }
        return result;
    }
}
