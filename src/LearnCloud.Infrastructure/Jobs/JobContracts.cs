using System.Text.Json;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.Infrastructure.Jobs;

public sealed class JobOptions
{
    /// <summary>Runs the job worker and the schedules in this process. Tests and local development turn it off.</summary>
    public bool Enabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxConcurrency { get; set; } = 4;
    /// <summary>How long a claim lasts; the worker renews it while the job runs.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Adds jobs to the queue.</summary>
public interface IBackgroundJobQueue
{
    /// <summary>
    /// Adds a job to the current database context. It becomes runnable when the caller calls
    /// SaveChanges, so it is committed or discarded together with the change that caused it.
    /// </summary>
    BackgroundJob Enqueue(string kind, object payload, long? tenantId, DateTime? runAfter = null, int maxAttempts = 8);
}

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly LearnCloudDbContext _db;

    public BackgroundJobQueue(LearnCloudDbContext db) => _db = db;

    public BackgroundJob Enqueue(string kind, object payload, long? tenantId, DateTime? runAfter = null, int maxAttempts = 8)
    {
        var now = DateTime.UtcNow;
        var job = new BackgroundJob
        {
            Kind = kind,
            TenantId = tenantId,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            Status = JobStatus.Pending,
            MaxAttempts = maxAttempts,
            RunAfter = runAfter ?? now,
            CreatedAt = now,
        };
        _db.Set<BackgroundJob>().Add(job);
        return job;
    }
}

/// <summary>Runs jobs of one kind. Registered as a scoped service by the module that owns the kind.</summary>
public interface IBackgroundJobHandler
{
    string Kind { get; }

    /// <summary>
    /// True when the payload holds personal data or secrets (email bodies, reset links). The
    /// worker replaces it once the job has finished, successfully or not.
    /// </summary>
    bool PayloadIsSensitive => false;

    /// <summary>
    /// Runs the job inside the job's tenant scope (or an audited system no-tenant scope for
    /// platform jobs). Throw to retry with backoff; throw <see cref="PermanentJobFailureException"/>
    /// when retrying cannot help.
    /// </summary>
    Task HandleAsync(BackgroundJobContext job, CancellationToken ct);
}

public sealed record BackgroundJobContext(long JobId, string Kind, long? TenantId, string PayloadJson, int Attempt)
{
    public T Payload<T>() => JsonSerializer.Deserialize<T>(PayloadJson, BackgroundJobQueue.Json)
        ?? throw new PermanentJobFailureException($"Job {JobId} has an empty payload.");
}

/// <summary>A failure that retrying will not fix, such as a rejected email address.</summary>
public sealed class PermanentJobFailureException : Exception
{
    public PermanentJobFailureException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>A recurring job. Its schedule is stored in the database and shared by all instances.</summary>
public interface IScheduledJob
{
    string Name { get; }
    TimeSpan Interval { get; }
    /// <summary>Delay before the very first run on a new database.</summary>
    TimeSpan FirstRunDelay => TimeSpan.FromMinutes(1);
    /// <summary>Runs in its own DI scope inside an audited system no-tenant scope.</summary>
    Task RunAsync(IServiceProvider services, CancellationToken ct);
}
