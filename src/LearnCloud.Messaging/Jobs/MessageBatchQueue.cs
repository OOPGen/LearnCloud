using LearnCloud.Infrastructure.Jobs;

namespace LearnCloud.Messaging.Jobs;

// Queues message batches for sending as durable background jobs.
//
// Batches used to go into an in-memory channel: a restart lost them (they stayed Queued
// forever), and only one API instance could send. Batches created by communication rules
// were never queued at all.
public interface IMessageBatchQueue
{
    /// <summary>
    /// Adds the send job to the current database context; call SaveChanges in the same unit of
    /// work that marks the batch Queued, so the status and the job are saved together.
    /// </summary>
    void Enqueue(long tenantId, long batchId);
}

public sealed class MessageBatchQueue : IMessageBatchQueue
{
    public const string JobKind = "messaging.send_batch";
    private readonly IBackgroundJobQueue _jobs;

    public MessageBatchQueue(IBackgroundJobQueue jobs) => _jobs = jobs;

    public void Enqueue(long tenantId, long batchId) =>
        _jobs.Enqueue(JobKind, new SendBatchPayload(batchId), tenantId, maxAttempts: 5);
}

public sealed record SendBatchPayload(long BatchId);

/// <summary>
/// Sends a queued batch. Runs in the batch's tenant scope. A batch interrupted part-way
/// (restart, lost lease) is resumed: only deliveries still Queued are sent.
/// </summary>
public sealed class SendMessageBatchJobHandler : IBackgroundJobHandler
{
    private readonly MessagingBackgroundJob _job;

    public SendMessageBatchJobHandler(MessagingBackgroundJob job) => _job = job;

    public string Kind => MessageBatchQueue.JobKind;

    public Task HandleAsync(BackgroundJobContext job, CancellationToken ct) =>
        _job.ProcessBatchAsync(job.Payload<SendBatchPayload>().BatchId, ct);
}
