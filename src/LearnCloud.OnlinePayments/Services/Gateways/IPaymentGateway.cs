namespace LearnCloud.OnlinePayments.Services.Gateways;

// IPaymentGateway abstraction supporting card, bank transfer and mobile money where available
public class InitiatePaymentRequest
{
    public long TenantId { get; set; }
    public long StudentId { get; set; }
    public long GuardianId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Method { get; set; } = "card"; // card, bank_transfer, mobile_money, ecocash, onemoney
    public string ClientReference { get; set; } = null!; // idempotency key, our reference
    public string IdempotencyKey { get; set; } = null!;
    public string? Description { get; set; }
    public string? ReturnUrl { get; set; } // where gateway redirects after payment (parent portal)
    public string? CancelUrl { get; set; }
    public Dictionary<string, string>? CustomerInfo { get; set; } // email, phone, name
}

public class InitiatePaymentResult
{
    public bool Success { get; set; }
    public string? PaymentUrl { get; set; } // redirect URL for card/bank transfer/mobile money
    public string? GatewayReference { get; set; } // gateway payment ID
    public string? FailureReason { get; set; }
    public string? QrCode { get; set; } // for mobile money USSD
}

public class VerifyWebhookRequest
{
    public string Payload { get; set; } = null!; // raw body
    public string? Signature { get; set; } // header X-Signature or similar
    public string? SignatureHeaderName { get; set; } = "X-Paynow-Signature";
    public Dictionary<string, string>? Headers { get; set; }
}

public class VerifyWebhookResult
{
    public bool IsValid { get; set; }
    public string? FailureReason { get; set; }
    public string? GatewayTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "succeeded"; // succeeded, failed, pending
    public string? ClientReference { get; set; } // our reference to match initiation
    public string? Method { get; set; }
    public string? ProviderReference { get; set; }
    public bool IsReplay { get; set; } = false;
}

public interface IPaymentGateway
{
    string GatewayName { get; }
    Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken ct = default);
    Task<VerifyWebhookResult> VerifyWebhookAsync(VerifyWebhookRequest request, CancellationToken ct = default);
    Task<decimal> CalculateFeeAsync(decimal amount, string method, CancellationToken ct = default);
    bool SupportsMethod(string method); // card, bank_transfer, mobile_money, ecocash etc.
}
