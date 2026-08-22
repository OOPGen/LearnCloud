using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.OnlinePayments.Entities;

public enum PaymentGatewayType { PayNow = 1, Stripe = 2, Manual = 3 }
public enum OnlinePaymentStatus { Initiated = 1, Pending = 2, Processing = 3, Succeeded = 4, Failed = 5, Cancelled = 6, Expired = 7 }
public enum GatewayTransactionStatus { Received = 1, Verified = 2, Matched = 3, Unmatched = 4, Failed = 5, Duplicate = 6 }

// An IPaymentGateway abstraction with one concrete implementation, chosen per tenant by configuration
public class PaymentGatewaySettings : TenantOwnedEntity
{
    public string GatewayName { get; set; } = "PayNow"; // PayNow supports card, bank transfer and mobile money where available
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public string? ConfigJson { get; set; } // {apiKey, merchantId, integrationId, webhookSecret, supportedMethods: ["card","bank_transfer","mobile_money"]}
    public string SupportedMethodsJson { get; set; } = "[\"card\",\"bank_transfer\",\"mobile_money\",\"ecocash\",\"onemoney\"]";
    public decimal FeePercentage { get; set; } = 2.5m; // gateway fee %
    public decimal FeeFixed { get; set; } = 0.10m;
    public string Currency { get; set; } = "USD";
    public bool EnableCard { get; set; } = true;
    public bool EnableBankTransfer { get; set; } = true;
    public bool EnableMobileMoney { get; set; } = true;
}

// Payment initiation from parent portal against outstanding balance, partial payment allowed
public class OnlinePaymentInitiation : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long GuardianId { get; set; }
    public long? InvoiceId { get; set; } // optional specific invoice, null = against outstanding balance
    public decimal RequestedAmount { get; set; } // partial allowed
    public string Currency { get; set; } = "USD";
    public string Method { get; set; } = "card"; // card, bank_transfer, mobile_money, ecocash
    public OnlinePaymentStatus Status { get; set; } = OnlinePaymentStatus.Initiated;
    public string? IdempotencyKey { get; set; } // for safe retry
    public string? ClientReference { get; set; } // our reference sent to gateway
    public string? GatewayReference { get; set; } // poll URL or payment ID from gateway
    public string? PaymentUrl { get; set; } // redirect URL for card/bank transfer
    public string? FailureReason { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public long CreatedByUserId { get; set; } // parent user
    public long? PaymentId { get; set; } // linked to Fees.Payment after success - automatic receipt generation
}

// Webhook handling idempotent, signature-verified, safe against replay and out-of-order
public class GatewayTransaction : TenantOwnedEntity
{
    public string GatewayName { get; set; } = "PayNow";
    public string? GatewayTransactionId { get; set; } // unique from gateway
    public string? ProviderReference { get; set; }
    public string PayloadJson { get; set; } = null!; // raw webhook body
    public string? Signature { get; set; } // received signature
    public bool IsSignatureVerified { get; set; } = false;
    public string Status { get; set; } = "received"; // received, verified, matched, unmatched, failed, duplicate
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Method { get; set; }
    public string? ClientReference { get; set; } // our idempotency/client ref to match initiation
    public long? MatchedInitiationId { get; set; }
    public long? MatchedPaymentId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public bool IsReplay { get; set; } = false; // detected duplicate
    public bool IsOutOfOrder { get; set; } = false; // webhook arrived before initiation committed
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
}

// Settlement and fee reporting so bursar can reconcile gateway payout against receipts
public class GatewaySettlement : TenantOwnedEntity
{
    public string SettlementId { get; set; } = null!; // from gateway payout report
    public DateTime SettlementDate { get; set; }
    public decimal GrossAmount { get; set; } // sum of payments in settlement
    public decimal FeeAmount { get; set; } // gateway fees
    public decimal NetAmount { get; set; } // gross - fee = payout
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending"; // pending, reconciled, discrepancy
    public string? RawDataJson { get; set; }
    public long? ReconciledByUserId { get; set; }
    public DateTime? ReconciledAt { get; set; }
}

public class SettlementTransaction : TenantOwnedEntity
{
    public long SettlementId { get; set; }
    public GatewaySettlement Settlement { get; set; } = null!;
    public long? GatewayTransactionId { get; set; }
    public GatewayTransaction? GatewayTransaction { get; set; }
    public long? PaymentId { get; set; } // Fees.Payment
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal Net { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsMatched { get; set; } = false;
}

// Reconciliation screen: gateway transactions against recorded payments
public class ReconciliationItem
{
    public long? GatewayTransactionId { get; set; }
    public long? PaymentId { get; set; }
    public decimal GatewayAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public string Status { get; set; } = "unmatched"; // matched, unmatched_gateway_only, unmatched_payment_only, amount_mismatch
}
