using System.Collections.Concurrent;
using LearnCloud.Infrastructure.Email;

namespace LearnCloud.IntegrationTests;

/// <summary>Stands in for the email provider: records what would be sent, and can be told to fail.</summary>
public sealed class CapturingEmailDelivery : IEmailDelivery
{
    private readonly ConcurrentQueue<EmailSendResult> _scriptedResults = new();

    public ConcurrentQueue<(OutgoingEmail Email, string? IdempotencyKey)> Sent { get; } = new();

    /// <summary>Every send attempt, including the ones told to fail.</summary>
    public ConcurrentQueue<(string To, string? IdempotencyKey)> Attempts { get; } = new();

    public string ProviderName => "Capture";
    public bool SendsRealEmail => true;

    /// <summary>The next send returns this result instead of succeeding.</summary>
    public void FailNext(EmailSendResult result) => _scriptedResults.Enqueue(result);

    public Task<EmailSendResult> SendAsync(OutgoingEmail email, string? idempotencyKey, CancellationToken ct)
    {
        Attempts.Enqueue((email.To, idempotencyKey));
        if (_scriptedResults.TryDequeue(out var scripted)) return Task.FromResult(scripted);
        Sent.Enqueue((email, idempotencyKey));
        return Task.FromResult(EmailSendResult.Sent($"capture-{Sent.Count}"));
    }

    public List<OutgoingEmail> To(string address) =>
        Sent.Where(s => string.Equals(s.Email.To, address, StringComparison.OrdinalIgnoreCase)).Select(s => s.Email).ToList();
}
