using System.Threading.Channels;

namespace LearnCloud.Messaging.Jobs;

// In-process queue of message batches waiting to be sent.
//
// MessagingController used to start the send with Task.Run, reusing the request's
// DbContext after the request had ended (the context is disposed by then) and
// swallowing every exception. Batches are now enqueued here and sent by
// MessagingQueueWorker, which gives each batch its own DI scope.
//
// Queued batches live in memory only; a restart loses the in-flight queue but not the
// batches, which stay in status Queued in the database. A durable job store is a
// later-phase decision.
public interface IMessageBatchQueue
{
    ValueTask EnqueueAsync(long batchId, CancellationToken ct = default);
    IAsyncEnumerable<long> DequeueAllAsync(CancellationToken ct);
}

public sealed class MessageBatchQueue : IMessageBatchQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(long batchId, CancellationToken ct = default) => _channel.Writer.WriteAsync(batchId, ct);

    public IAsyncEnumerable<long> DequeueAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
