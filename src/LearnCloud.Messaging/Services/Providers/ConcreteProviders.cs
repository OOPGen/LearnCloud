using LearnCloud.Messaging.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LearnCloud.Messaging.Services.Providers;

// Concrete SMS implementation - Zimbabwe-friendly, swappable via settings
// For V1: EcoCash SMS / BulkSmsZw style HTTP API mock, but structure allows Twilio, etc.

public class SmsProviderOptions
{
    public string ProviderName { get; set; } = "EcoCashSms";
    public string ApiKey { get; set; } = null!; // SECURITY: Must be set via Sms__ApiKey env, no demo default

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("SECURITY: SmsProvider ApiKey must be set via env Sms__ApiKey - no demo default");
        if (ApiKey == "[REDACTED_MUST_BE_SET_VIA_ENV]" || ApiKey.ToLower().Contains("demo"))
            throw new InvalidOperationException("SECURITY: SmsProvider demo key detected - must use real API key");
    }
    public string ApiUrl { get; set; } = "https://api.sms.co.zw/send";
    public string SenderId { get; set; } = "LearnCloud";
    public decimal CostPerSms { get; set; } = 0.05m;
    public string Currency { get; set; } = "USD";
}

public class EcoCashSmsProvider : ISmsProvider
{
    public string ProviderName => "EcoCashSms";
    private readonly SmsProviderOptions _options;
    private readonly ILogger<EcoCashSmsProvider> _logger;
    private readonly HttpClient _http;

    public EcoCashSmsProvider(IOptions<SmsProviderOptions> options, ILogger<EcoCashSmsProvider> logger, HttpClient http)
    {
        _options = options.Value;
        _options.Validate();
        _logger = logger;
        _http = http;
    }

    public async Task<SmsResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        // In real implementation, call HTTP API:
        // var payload = new { api_key = _options.ApiKey, to = message.To, from = message.From ?? _options.SenderId, body = message.Body };
        // var res = await _http.PostAsJsonAsync(_options.ApiUrl, payload, ct);
        // For V1 mock, simulate success with 95% rate, random provider ref

        // Simulate network delay for rate limiting
        await Task.Delay(50, ct);

        // Simulate occasional failure for retry logic
        var random = new Random();
        if (random.Next(0, 100) < 5) // 5% failure
        {
            _logger.LogWarning("SMS provider {Provider} failed to {To}: simulated transient failure", ProviderName, message.To);
            return new SmsResult { Success = false, FailureReason = "Provider transient error - timeout", Cost = 0m, Currency = _options.Currency };
        }

        var providerRef = $"SMS-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        _logger.LogInformation("SMS sent via {Provider} to {To} ref {Ref} cost {Cost} {Currency}", ProviderName, message.To, providerRef, _options.CostPerSms, _options.Currency);

        // Never log body with PII in prod? For V1 we log truncated
        return new SmsResult
        {
            Success = true,
            ProviderReference = providerRef,
            Cost = _options.CostPerSms,
            Currency = _options.Currency
        };
    }

    public Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        // Mock balance
        return Task.FromResult(1000m);
    }
}

// Alternative SMS provider example - BulkSmsZw - swappable via config
public class BulkSmsZwProvider : ISmsProvider
{
    public string ProviderName => "BulkSmsZw";
    private readonly ILogger<BulkSmsZwProvider> _logger;

    public BulkSmsZwProvider(ILogger<BulkSmsZwProvider> logger) => _logger = logger;

    public async Task<SmsResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        await Task.Delay(80, ct);
        _logger.LogInformation("SMS via BulkSmsZw to {To}", message.To);
        return new SmsResult { Success = true, ProviderReference = $"BULK-{Guid.NewGuid():N}"[..10], Cost = 0.04m, Currency = "USD" };
    }

    public Task<decimal> GetBalanceAsync(CancellationToken ct = default) => Task.FromResult(500m);
}

// Concrete Email implementation - Smtp / SendGrid swappable

public class EmailProviderOptions
{
    public string ProviderName { get; set; } = "Smtp";
    public string FromEmail { get; set; } = "noreply@learncloud.co.zw";
    public string FromName { get; set; } = "LearnCloud";
    public string SmtpHost { get; set; } = null!; // SECURITY: Must be set via Email__SmtpHost env
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = null!; // SECURITY: Must be set via env
    public string SmtpPass { get; set; } = null!; // SECURITY: Must be set via env

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SmtpHost))
            throw new InvalidOperationException("SECURITY: Email SmtpHost must be set via env");
        if (SmtpUser == "user" || SmtpPass == "pass")
            throw new InvalidOperationException("SECURITY: Email SmtpUser/Pass demo values detected - must use real credentials via env");
    }
    public decimal CostPerEmail { get; set; } = 0.01m;
    public string Currency { get; set; } = "USD";
}

public class SmtpEmailProvider : IEmailProvider
{
    public string ProviderName => "Smtp";
    private readonly EmailProviderOptions _options;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(IOptions<EmailProviderOptions> options, ILogger<SmtpEmailProvider> logger)
    {
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // Real implementation would use MailKit SmtpClient
        await Task.Delay(100, ct);

        var random = new Random();
        if (random.Next(0, 100) < 3) // 3% failure
        {
            _logger.LogWarning("Email provider {Provider} failed to {To}", ProviderName, message.To);
            return new EmailResult { Success = false, FailureReason = "SMTP transient error", Cost = 0m, Currency = _options.Currency };
        }

        var refId = $"EMAIL-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        _logger.LogInformation("Email sent via {Provider} to {To} subject {Subject} ref {Ref}", ProviderName, message.To, message.Subject, refId);

        return new EmailResult
        {
            Success = true,
            ProviderReference = refId,
            Cost = _options.CostPerEmail,
            Currency = _options.Currency
        };
    }
}

public class SendGridEmailProvider : IEmailProvider
{
    public string ProviderName => "SendGrid";
    private readonly ILogger<SendGridEmailProvider> _logger;

    public SendGridEmailProvider(ILogger<SendGridEmailProvider> logger) => _logger = logger;

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        await Task.Delay(120, ct);
        _logger.LogInformation("Email via SendGrid to {To}", message.To);
        return new EmailResult { Success = true, ProviderReference = $"SG-{Guid.NewGuid():N}"[..10], Cost = 0.005m, Currency = "USD" };
    }
}

// Factory that resolves provider by settings so provider can be swapped without touching calling code
public interface IMessagingProviderFactory
{
    Task<ISmsProvider> GetSmsProviderAsync(long tenantId, CancellationToken ct = default);
    Task<IEmailProvider> GetEmailProviderAsync(long tenantId, CancellationToken ct = default);
}

public class MessagingProviderFactory : IMessagingProviderFactory
{
    private readonly IServiceProvider _sp;
    private readonly LearnCloud.MultiTenancy.Context.LearnCloudDbContext _db;

    public MessagingProviderFactory(IServiceProvider sp, LearnCloud.MultiTenancy.Context.LearnCloudDbContext db)
    {
        _sp = sp;
        _db = db;
    }

    public async Task<ISmsProvider> GetSmsProviderAsync(long tenantId, CancellationToken ct = default)
    {
        var settings = await _db.Set<MessagingProviderSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Channel == MessageChannel.Sms && s.IsActive && !s.IsDeleted, ct);
        var providerName = settings?.ProviderName ?? "EcoCashSms";

        // Resolve by name via DI keyed services (simplified switch for V1)
        return providerName switch
        {
            "BulkSmsZw" => (ISmsProvider)_sp.GetService(typeof(BulkSmsZwProvider))!,
            _ => (ISmsProvider)_sp.GetService(typeof(EcoCashSmsProvider))!
        };
    }

    public async Task<IEmailProvider> GetEmailProviderAsync(long tenantId, CancellationToken ct = default)
    {
        var settings = await _db.Set<MessagingProviderSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Channel == MessageChannel.Email && s.IsActive && !s.IsDeleted, ct);
        var providerName = settings?.ProviderName ?? "Smtp";

        return providerName switch
        {
            "SendGrid" => (IEmailProvider)_sp.GetService(typeof(SendGridEmailProvider))!,
            _ => (IEmailProvider)_sp.GetService(typeof(SmtpEmailProvider))!
        };
    }
}
