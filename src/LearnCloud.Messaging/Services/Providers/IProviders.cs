using LearnCloud.Messaging.Entities;

namespace LearnCloud.Messaging.Services.Providers;

// Provider abstraction for the message batch sender. Email goes through the platform's
// email delivery (LearnCloud.Infrastructure); no SMS provider is integrated yet.

public class SmsMessage
{
    public string To { get; set; } = null!; // phone number E.164
    public string Body { get; set; } = null!;
    public string? From { get; set; } // sender ID
    public string? CallbackUrl { get; set; }
}

public class SmsResult
{
    public bool Success { get; set; }
    public string? ProviderReference { get; set; } // provider's message ID
    public string? FailureReason { get; set; }
    /// <summary>True when trying again later may succeed (timeouts, rate limits).</summary>
    public bool IsTransient { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";
}

public interface ISmsProvider
{
    string ProviderName { get; }
    Task<SmsResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}

public class EmailMessage
{
    public string To { get; set; } = null!;
    public string? ToName { get; set; }
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string? TextBody { get; set; }
    public string? From { get; set; }
    public string? FromName { get; set; }
    public List<EmailAttachment>? Attachments { get; set; }
}

public class EmailAttachment
{
    public string FileName { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
    public string ContentType { get; set; } = "application/octet-stream";
}

public class EmailResult
{
    public bool Success { get; set; }
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }
    /// <summary>True when trying again later may succeed (timeouts, rate limits).</summary>
    public bool IsTransient { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";
}

public interface IEmailProvider
{
    string ProviderName { get; }
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}
