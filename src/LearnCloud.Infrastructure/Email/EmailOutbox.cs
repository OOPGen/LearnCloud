using System.Net;
using System.Text;
using LearnCloud.Infrastructure.Jobs;
using Microsoft.Extensions.Hosting;

namespace LearnCloud.Infrastructure.Email;

/// <summary>Queues emails as background jobs, so a slow or failing provider never blocks a request.</summary>
public interface IEmailOutbox
{
    /// <summary>Adds the email to the current database context; it is sent after the caller's SaveChanges.</summary>
    void Queue(OutgoingEmail email, long? tenantId);
}

public sealed class EmailOutbox : IEmailOutbox
{
    public const string JobKind = "email.send";
    private readonly IBackgroundJobQueue _jobs;

    public EmailOutbox(IBackgroundJobQueue jobs) => _jobs = jobs;

    // The key is random rather than the job id: job ids repeat across databases (staging and
    // production, or after a reset), and the provider would drop a "duplicate".
    public void Queue(OutgoingEmail email, long? tenantId) =>
        _jobs.Enqueue(JobKind, email with { MessageKey = email.MessageKey ?? $"learncloud-{Guid.NewGuid():N}" }, tenantId, maxAttempts: 8);
}

/// <summary>Sends queued emails. Payloads are cleared afterwards: they can hold reset links.</summary>
public sealed class EmailJobHandler : IBackgroundJobHandler
{
    private readonly IEmailDelivery _delivery;
    private readonly ILogger<EmailJobHandler> _logger;

    public EmailJobHandler(IEmailDelivery delivery, ILogger<EmailJobHandler> logger)
    {
        _delivery = delivery;
        _logger = logger;
    }

    public string Kind => EmailOutbox.JobKind;
    public bool PayloadIsSensitive => true;

    public async Task HandleAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var email = job.Payload<OutgoingEmail>();
        var result = await _delivery.SendAsync(email, email.MessageKey ?? $"learncloud-email-job-{job.JobId}", ct);
        if (result.Success)
        {
            _logger.LogInformation("Email job {JobId} ({Category}) sent via {Provider}, reference {Reference}", job.JobId, email.Category, _delivery.ProviderName, result.ProviderMessageId);
            return;
        }
        if (result.IsTransient) throw new InvalidOperationException(result.Error);
        throw new PermanentJobFailureException(result.Error ?? "The provider refused the email.");
    }
}

/// <summary>Public addresses of the web app, section "App".</summary>
public sealed class AppOptions
{
    /// <summary>
    /// The web app's address, used in links sent by email, e.g. https://app.learncloud.co.zw.
    /// Deliberately empty in appsettings.json: outside Development the API warns until it is set.
    /// </summary>
    public string PublicUrl { get; set; } = "http://localhost:5173";

    public string Link(string path, IReadOnlyDictionary<string, string?> query)
    {
        var qs = string.Join("&", query.Where(kv => !string.IsNullOrEmpty(kv.Value)).Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return $"{PublicUrl.TrimEnd('/')}/{path.TrimStart('/')}{(qs.Length > 0 ? "?" + qs : "")}";
    }
}

/// <summary>A plain, readable HTML email with an optional button. All text is HTML-encoded.</summary>
public static class EmailLayout
{
    public static (string Html, string Text) Render(string heading, IEnumerable<string> paragraphs, string? buttonText = null, string? buttonUrl = null, string? footer = null)
    {
        var paras = paragraphs.ToList();
        var html = new StringBuilder();
        html.Append("<!doctype html><html><body style=\"margin:0;background:#f5f5f7;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#1d1d1f\">");
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td align=\"center\" style=\"padding:24px\">");
        html.Append("<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#ffffff;border-radius:12px;padding:32px\"><tr><td>");
        html.Append("<p style=\"margin:0 0 24px;font-weight:700;color:#0F153A\">LearnCloud</p>");
        html.Append($"<h1 style=\"margin:0 0 16px;font-size:20px\">{Enc(heading)}</h1>");
        foreach (var p in paras) html.Append($"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.5\">{Enc(p)}</p>");
        if (buttonText is not null && buttonUrl is not null)
        {
            html.Append($"<p style=\"margin:24px 0\"><a href=\"{Enc(buttonUrl)}\" style=\"background:#0F153A;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;display:inline-block;font-weight:600\">{Enc(buttonText)}</a></p>");
            html.Append($"<p style=\"margin:0 0 16px;font-size:13px;color:#6e6e73\">If the button does not work, open this address: <br><span style=\"word-break:break-all\">{Enc(buttonUrl)}</span></p>");
        }
        if (footer is not null) html.Append($"<p style=\"margin:24px 0 0;font-size:12px;color:#6e6e73\">{Enc(footer)}</p>");
        html.Append("</td></tr></table></td></tr></table></body></html>");

        var text = new StringBuilder().AppendLine(heading).AppendLine();
        foreach (var p in paras) text.AppendLine(p).AppendLine();
        if (buttonText is not null && buttonUrl is not null) text.AppendLine($"{buttonText}: {buttonUrl}").AppendLine();
        if (footer is not null) text.AppendLine(footer);
        return (html.ToString(), text.ToString());
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}

/// <summary>Warns at startup when email is misconfigured or not being sent.</summary>
public sealed class EmailConfigurationCheck : IHostedService
{
    private readonly IEmailDelivery _delivery;
    private readonly IHostEnvironment _environment;
    private readonly AppOptions _app;
    private readonly ILogger<EmailConfigurationCheck> _logger;

    public EmailConfigurationCheck(IServiceScopeFactory scopes, IHostEnvironment environment, IOptions<AppOptions> app, ILogger<EmailConfigurationCheck> logger)
    {
        using var scope = scopes.CreateScope();
        _delivery = scope.ServiceProvider.GetRequiredService<IEmailDelivery>();
        _environment = environment;
        _app = app.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_delivery.SendsRealEmail && !_environment.IsDevelopment())
            _logger.LogWarning("Email:Provider is Log: password reset, verification and billing emails are NOT being sent. Configure Resend or SMTP (docs/DEPLOYMENT.md).");
        else
            _logger.LogInformation("Email delivery provider: {Provider}", _delivery.ProviderName);

        if (!_environment.IsDevelopment() && LooksLocal(_app.PublicUrl))
            _logger.LogWarning("App:PublicUrl is '{PublicUrl}', not a public address: links in password reset and verification emails will not work. Set App__PublicUrl to the web app's address.", _app.PublicUrl);
        return Task.CompletedTask;
    }

    internal static bool LooksLocal(string url) =>
        !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.IsLoopback || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
