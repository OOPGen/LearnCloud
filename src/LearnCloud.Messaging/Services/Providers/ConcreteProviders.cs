using LearnCloud.Infrastructure.Email;
using LearnCloud.Messaging.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LearnCloud.Messaging.Services.Providers;

// The providers that used to be here (EcoCashSms, BulkSmsZw, Smtp, SendGrid) sent nothing:
// they waited briefly, failed at random 3-5% of the time and otherwise reported success
// with an invented reference, so schools would have been told messages went out.

public class MessagingCostOptions
{
    /// <summary>What the school is charged per email, for usage reports. Section Messaging:Costs.</summary>
    public decimal PerEmail { get; set; } = 0m;
    public string Currency { get; set; } = "USD";
}

/// <summary>Email for message batches, through the platform's configured email delivery.</summary>
public class PlatformEmailProvider : IEmailProvider
{
    private readonly IEmailDelivery _delivery;
    private readonly MessagingCostOptions _costs;

    public PlatformEmailProvider(IEmailDelivery delivery, IOptions<MessagingCostOptions> costs)
    {
        _delivery = delivery;
        _costs = costs.Value;
    }

    public string ProviderName => _delivery.ProviderName;

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var result = await _delivery.SendAsync(
            new OutgoingEmail(message.To, message.ToName, message.Subject, message.HtmlBody, message.TextBody, Category: "school-message"),
            idempotencyKey: null, ct);
        return new EmailResult
        {
            Success = result.Success,
            ProviderReference = result.ProviderMessageId,
            FailureReason = result.Error,
            IsTransient = result.IsTransient,
            Cost = result.Success ? _costs.PerEmail : 0m,
            Currency = _costs.Currency,
        };
    }
}

/// <summary>
/// Used until an SMS provider is integrated: every SMS fails with a clear reason instead of
/// pretending to be sent. Retrying cannot help, so it is not retried.
/// </summary>
public class UnconfiguredSmsProvider : ISmsProvider
{
    public const string Reason = "SMS is not available yet: no SMS provider is configured for LearnCloud.";
    private readonly ILogger<UnconfiguredSmsProvider> _logger;

    public UnconfiguredSmsProvider(ILogger<UnconfiguredSmsProvider> logger) => _logger = logger;

    public string ProviderName => "none";

    public Task<SmsResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        _logger.LogWarning("SMS not sent: no SMS provider is configured");
        return Task.FromResult(new SmsResult { Success = false, FailureReason = Reason, IsTransient = false });
    }
}

// Resolves the provider for a school's batch. Per-school provider settings
// (MessagingProviderSettings) still hold rate limits and caps; the provider itself is the
// platform's.
public interface IMessagingProviderFactory
{
    Task<ISmsProvider> GetSmsProviderAsync(long tenantId, CancellationToken ct = default);
    Task<IEmailProvider> GetEmailProviderAsync(long tenantId, CancellationToken ct = default);
}

public class MessagingProviderFactory : IMessagingProviderFactory
{
    private readonly IServiceProvider _sp;

    public MessagingProviderFactory(IServiceProvider sp) => _sp = sp;

    public Task<ISmsProvider> GetSmsProviderAsync(long tenantId, CancellationToken ct = default) =>
        Task.FromResult<ISmsProvider>(_sp.GetRequiredService<UnconfiguredSmsProvider>());

    public Task<IEmailProvider> GetEmailProviderAsync(long tenantId, CancellationToken ct = default) =>
        Task.FromResult<IEmailProvider>(_sp.GetRequiredService<PlatformEmailProvider>());
}
