namespace LearnCloud.Communication.Services;

/// <summary>
/// Settings for the SMS provider's inbound webhook, section Messaging:InboundSms.
/// </summary>
public sealed class InboundSmsOptions
{
    /// <summary>
    /// Shared secret the provider sends in X-LearnCloud-Webhook-Key. Empty (the default)
    /// closes the endpoint: no SMS provider is connected yet, so nothing should be accepted.
    /// </summary>
    public string? WebhookSecret { get; set; }
}
