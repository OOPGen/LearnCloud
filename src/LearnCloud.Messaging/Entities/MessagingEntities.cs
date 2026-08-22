using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Messaging.Entities;

public enum MessageChannel { Sms = 1, Email = 2, Portal = 3 }
public enum MessageStatus { Draft = 1, Queued = 2, Sending = 3, Sent = 4, Delivered = 5, Failed = 6, Cancelled = 7 }
public enum AudienceType { Class = 1, Stream = 2, YearGroup = 3, AllGuardians = 4, ArrearsOverX = 5, AbsentToday = 6, DynamicList = 7, Manual = 8 }

// Templates with merge fields (learner name, class, amount owed, date)
public class MessageTemplate : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // fee_reminder, absence_notification, general_notice
    public string Code { get; set; } = null!; // unique per tenant
    public MessageChannel Channel { get; set; } // Sms, Email, Both? For V1 template per channel or generic
    public string Subject { get; set; } = ""; // for email, or title for SMS
    public string Body { get; set; } = null!; // with merge fields {{learner_name}}, {{class}}, {{amount_owed}}, {{date}}, {{school_name}}
    public bool IsSystem { get; set; } = false; // seeded templates protected
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? MergeFieldsJson { get; set; } // JSON list of available merge fields for this template
}

// Message batch / campaign
public class MessageBatch : TenantOwnedEntity
{
    public string BatchNumber { get; set; } = null!; // MSG-2026-00001
    public string Title { get; set; } = null!; // internal title
    public long? TemplateId { get; set; }
    public MessageTemplate? Template { get; set; }

    public MessageChannel Channel { get; set; } // Sms or Email
    public AudienceType AudienceType { get; set; }
    public string AudienceFilterJson { get; set; } = "{}"; // e.g. {"gradeId":5,"streamId":10,"arrearsThreshold":100,"date":"2026-08-02"}

    public string Body { get; set; } = null!; // final body after template render? Or template body snapshot
    public string? Subject { get; set; }

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }

    public decimal EstimatedCost { get; set; } // DECIMAL(18,2)
    public decimal ActualCost { get; set; }
    public string Currency { get; set; } = "USD";

    public MessageStatus Status { get; set; } = MessageStatus.Draft; // draft, queued, sending, sent, failed
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public long CreatedByUserId { get; set; } // sender
    public string? CostEstimateJson { get; set; } // breakdown for confirmation screen

    public bool IsPreviewed { get; set; } = false;
}

// Delivery log per message: recipient, channel, status, provider reference, cost, timestamp
public class MessageDeliveryLog : TenantOwnedEntity
{
    public long BatchId { get; set; }
    public MessageBatch Batch { get; set; } = null!;

    public long? GuardianId { get; set; }
    public long? StudentId { get; set; } // for context, which learner this guardian message is about

    public string RecipientName { get; set; } = null!;
    public string RecipientAddress { get; set; } = null!; // phone number or email

    public MessageChannel Channel { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.Queued;
    public string? Provider { get; set; } // e.g. EcoCashSms, BulkSmsZw, SendGrid
    public string? ProviderReference { get; set; } // provider's message ID
    public decimal Cost { get; set; } // per message cost
    public string Currency { get; set; } = "USD";

    public string RenderedBody { get; set; } = null!; // final body with merge fields resolved for this recipient
    public string? RenderedSubject { get; set; }

    public int RetryCount { get; set; } = 0;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? FailureReason { get; set; }

    public bool IsOptedOut { get; set; } = false; // if guardian opted out at send time
}

// Per-tenant usage counter for billing SMS bundles
public class TenantMessagingUsage : TenantOwnedEntity
{
    public int Year { get; set; }
    public int Month { get; set; } // 1-12
    public int SmsCount { get; set; }
    public int EmailCount { get; set; }
    public decimal SmsCost { get; set; }
    public decimal EmailCost { get; set; }
    public int SmsLimit { get; set; } = 1000; // hard cap, configurable per tenant subscription
    public int EmailLimit { get; set; } = 5000;
    public string Currency { get; set; } = "USD";
}

// Guardian contact preferences and opt-out that is always honoured
public class GuardianContactPreference : TenantOwnedEntity
{
    public long GuardianId { get; set; }
    public bool SmsOptIn { get; set; } = true;
    public bool EmailOptIn { get; set; } = true;
    public bool SmsOptOut { get; set; } = false; // hard opt-out
    public bool EmailOptOut { get; set; } = false;
    public DateTime? SmsOptOutAt { get; set; }
    public DateTime? EmailOptOutAt { get; set; }
    public string? OptOutReason { get; set; }
    public string? PreferredLanguage { get; set; } = "en";
}

// Provider settings so provider can be swapped without touching calling code
public class MessagingProviderSettings : TenantOwnedEntity
{
    public MessageChannel Channel { get; set; }
    public string ProviderName { get; set; } = null!; // EcoCashSms, Twilio, BulkSmsZw, Smtp, SendGrid
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public string? ConfigJson { get; set; } // JSON: apiKey, senderId, smtp host, etc. encrypted in real app
    public decimal CostPerSms { get; set; } = 0.05m; // USD
    public decimal CostPerEmail { get; set; } = 0.01m;
    public string Currency { get; set; } = "USD";
    public int RateLimitPerSecond { get; set; } = 10; // per-tenant rate limit
    public int DailyCap { get; set; } = 1000; // hard per-tenant sending cap
}

// For cost estimate shown before confirming bulk send
public class CostEstimate
{
    public int TotalRecipients { get; set; }
    public int SmsRecipients { get; set; }
    public int EmailRecipients { get; set; }
    public int FilteredOutOptOut { get; set; }
    public int FilteredOutNoContact { get; set; }
    public decimal CostPerSms { get; set; }
    public decimal CostPerEmail { get; set; }
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = "USD";
    public bool CapWarning { get; set; } // true if near cap
    public string? CapWarningMessage { get; set; }
    public bool CapExceeded { get; set; } // hard cap would be exceeded
    public int RemainingAfterSend { get; set; } // remaining quota after this send
}
