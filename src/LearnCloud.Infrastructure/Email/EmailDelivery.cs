using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LearnCloud.Infrastructure.Email;

/// <param name="MessageKey">Unique per email and stable across retries; set by the outbox. Used as the
/// provider's idempotency key, so a retried email is not delivered twice.</param>
public sealed record OutgoingEmail(string To, string? ToName, string Subject, string HtmlBody, string? TextBody, string? ReplyTo = null, string? Category = null, string? MessageKey = null);

public sealed record EmailSendResult(bool Success, string? ProviderMessageId, string? Error, bool IsTransient)
{
    public static EmailSendResult Sent(string? id) => new(true, id, null, false);
    public static EmailSendResult Transient(string error) => new(false, null, error, true);
    public static EmailSendResult Permanent(string error) => new(false, null, error, false);
}

/// <summary>Sends one email through the configured provider.</summary>
public interface IEmailDelivery
{
    string ProviderName { get; }
    /// <summary>False for the development log provider: nothing leaves the server.</summary>
    bool SendsRealEmail { get; }
    /// <param name="idempotencyKey">The same key for retries of the same email, so a provider that supports it sends it once.</param>
    Task<EmailSendResult> SendAsync(OutgoingEmail email, string? idempotencyKey, CancellationToken ct);
}

/// <summary>Email settings, section "Email".</summary>
public sealed class EmailOptions
{
    /// <summary>Log (development: nothing is sent), Resend (HTTPS API) or Smtp.</summary>
    public string Provider { get; set; } = "Log";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "LearnCloud";
    public string? ReplyTo { get; set; }
    public ResendOptions Resend { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();

    public sealed class ResendOptions
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://api.resend.com/";
    }

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        /// <summary>Auto, StartTls, SslOnConnect or None (None only for a local mail catcher).</summary>
        public string Security { get; set; } = "Auto";
    }

    /// <summary>Configuration errors that make sending impossible; empty when usable.</summary>
    public IEnumerable<string> Problems()
    {
        var provider = Provider.Trim().ToLowerInvariant();
        if (provider is not ("log" or "resend" or "smtp"))
            yield return $"Email:Provider must be Log, Resend or Smtp (was '{Provider}').";
        if (provider is "resend" or "smtp")
        {
            if (!MailboxAddress.TryParse(FromAddress, out _) || !FromAddress.Contains('@'))
                yield return "Email:FromAddress must be a sender address on a domain verified with the provider.";
            if (provider == "resend" && string.IsNullOrWhiteSpace(Resend.ApiKey))
                yield return "Email:Resend:ApiKey is required when Email:Provider is Resend.";
            if (provider == "smtp" && string.IsNullOrWhiteSpace(Smtp.Host))
                yield return "Email:Smtp:Host is required when Email:Provider is Smtp.";
            if (provider == "smtp" && !(Enum.TryParse<SecureSocketOptions>(Smtp.Security, ignoreCase: true, out var security)
                    && !int.TryParse(Smtp.Security, out _) && Enum.IsDefined(security)))
                yield return "Email:Smtp:Security must be Auto, StartTls, SslOnConnect or None.";
        }
    }
}

internal static class EmailAddresses
{
    /// <summary>The address as a mailbox, or null unless it parses and has a local part and a domain.</summary>
    public static MailboxAddress? Usable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !MailboxAddress.TryParse(value, out var mailbox)) return null;
        var at = mailbox.Address.IndexOf('@');
        return at > 0 && at < mailbox.Address.Length - 1 ? mailbox : null;
    }
}

/// <summary>Development provider: records that an email would have been sent, without its body.</summary>
public sealed class LogEmailDelivery : IEmailDelivery
{
    private readonly ILogger<LogEmailDelivery> _logger;

    public LogEmailDelivery(ILogger<LogEmailDelivery> logger) => _logger = logger;

    public string ProviderName => "Log";
    public bool SendsRealEmail => false;

    public Task<EmailSendResult> SendAsync(OutgoingEmail email, string? idempotencyKey, CancellationToken ct)
    {
        // The body is not logged: it can hold sign-in and password reset links.
        _logger.LogInformation("Email NOT sent (Email:Provider=Log) to {Recipient}: {Subject}", MaskAddress(email.To), email.Subject);
        return Task.FromResult(EmailSendResult.Sent($"log-{Guid.NewGuid():N}"));
    }

    internal static string MaskAddress(string address)
    {
        var at = address.IndexOf('@');
        return at <= 1 ? "***" : $"{address[0]}***{address[at..]}";
    }
}

/// <summary>
/// Resend's HTTPS API (https://resend.com/docs/api-reference/emails/send-email). Works on
/// hosts that block outbound SMTP, such as Railway's Free and Hobby plans.
/// </summary>
public sealed class ResendEmailDelivery : IEmailDelivery
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly HttpClient _http;
    private readonly EmailOptions _options;

    public ResendEmailDelivery(HttpClient http, IOptions<EmailOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string ProviderName => "Resend";
    public bool SendsRealEmail => true;

    public async Task<EmailSendResult> SendAsync(OutgoingEmail email, string? idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new ResendEmail(
                From: new MailboxAddress(_options.FromName, _options.FromAddress).ToString(),
                To: new[] { email.To },
                Subject: email.Subject,
                Html: email.HtmlBody,
                Text: email.TextBody,
                ReplyTo: EmailAddresses.Usable(email.ReplyTo ?? _options.ReplyTo)?.Address,
                Tags: email.Category is null ? null : new[] { new ResendTag("category", SafeTag(email.Category)) }), options: Json),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);
        if (!string.IsNullOrEmpty(idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return EmailSendResult.Transient($"Resend could not be reached: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                string? id = null;
                try { id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetString(); } catch (Exception) { }
                return EmailSendResult.Sent(id);
            }

            var detail = $"Resend answered {(int)response.StatusCode}: {(body.Length > 500 ? body[..500] : body)}";
            var transient = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)response.StatusCode >= 500;
            return transient ? EmailSendResult.Transient(detail) : EmailSendResult.Permanent(detail);
        }
    }

    // Resend tag values allow ASCII letters, digits, underscores and dashes.
    private static string SafeTag(string value) => new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_').Take(256).ToArray());

    private sealed record ResendEmail(string From, string[] To, string Subject, string Html, string? Text,
        [property: JsonPropertyName("reply_to")] string? ReplyTo, ResendTag[]? Tags);

    private sealed record ResendTag(string Name, string Value);
}

/// <summary>SMTP through MailKit, for hosts that allow outbound SMTP and for a local mail catcher.</summary>
public sealed class SmtpEmailDelivery : IEmailDelivery
{
    private readonly EmailOptions _options;

    public SmtpEmailDelivery(IOptions<EmailOptions> options) => _options = options.Value;

    public string ProviderName => "Smtp";
    public bool SendsRealEmail => true;

    public async Task<EmailSendResult> SendAsync(OutgoingEmail email, string? idempotencyKey, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        try { message.To.Add(new MailboxAddress(email.ToName ?? "", email.To)); }
        catch (ParseException ex) { return EmailSendResult.Permanent($"Invalid recipient address: {ex.Message}"); }
        // An unusable reply-to address (it can come from a web form) must not stop the email.
        if (EmailAddresses.Usable(email.ReplyTo ?? _options.ReplyTo) is { } replyTo) message.ReplyTo.Add(replyTo);
        message.Subject = email.Subject;
        if (idempotencyKey is not null)
            message.MessageId = $"{idempotencyKey}@{_options.FromAddress.Split('@').Last()}";
        message.Body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = email.TextBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 30_000 };
        try
        {
            await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, Enum.Parse<SecureSocketOptions>(_options.Smtp.Security, ignoreCase: true), ct);
            if (!string.IsNullOrEmpty(_options.Smtp.Username))
                await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password ?? "", ct);
            var response = await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);
            return EmailSendResult.Sent(message.MessageId ?? response);
        }
        catch (AuthenticationException ex)
        {
            return EmailSendResult.Permanent($"SMTP authentication failed: {ex.Message}");
        }
        catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
        {
            return EmailSendResult.Permanent($"SMTP server refused the email ({(int)ex.StatusCode}): {ex.Message}");
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or IOException or System.Net.Sockets.SocketException or TimeoutException
                                   || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return EmailSendResult.Transient($"SMTP delivery failed: {ex.Message}");
        }
    }
}
