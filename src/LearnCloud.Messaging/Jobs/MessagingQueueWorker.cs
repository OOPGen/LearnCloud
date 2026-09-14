using Microsoft.Extensions.Hosting;

namespace LearnCloud.Messaging.Jobs;

// Sends queued message batches one at a time, each in a fresh DI scope so it gets its
// own DbContext and tenant context. MessagingBackgroundJob resolves the batch's tenant
// itself and scopes all further queries to it.
public sealed class MessagingQueueWorker : BackgroundService
{
    private readonly IMessageBatchQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessagingQueueWorker> _logger;

    public MessagingQueueWorker(IMessageBatchQueue queue, IServiceScopeFactory scopeFactory, ILogger<MessagingQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var batchId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var job = scope.ServiceProvider.GetRequiredService<MessagingBackgroundJob>();
                await job.ProcessBatchAsync(batchId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sending message batch {BatchId} failed", batchId);
            }
        }
    }
}
