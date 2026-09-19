using System.Linq.Expressions;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace LearnCloud.Infrastructure.Jobs;

/// <summary>
/// Claims and runs queued background jobs. Claiming is a conditional UPDATE: PostgreSQL
/// re-checks the condition under the row lock, so when several instances race for a job only
/// one update succeeds. A claim is a lease; if the instance dies, the job becomes claimable
/// again when the lease runs out.
/// </summary>
public sealed class BackgroundJobRunner
{
    internal const string RedactedPayload = "{\"redacted\":true}";
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BackgroundJobRunner> _logger;
    private readonly JobOptions _options;

    public BackgroundJobRunner(IServiceScopeFactory scopes, ILogger<BackgroundJobRunner> logger, IOptions<JobOptions> options)
    {
        _scopes = scopes;
        _logger = logger;
        _options = options.Value;
    }

    internal string InstanceId { get; } = $"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid():N}";

    private static Expression<Func<BackgroundJob, bool>> Due(DateTime now) => j =>
        (j.Status == JobStatus.Pending && j.RunAfter <= now) || (j.Status == JobStatus.Running && j.LockedUntil < now);

    /// <summary>Claims the next due job, or returns null when nothing is due.</summary>
    public async Task<BackgroundJob?> TryClaimAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var now = DateTime.UtcNow;
        var candidates = await db.Set<BackgroundJob>().AsNoTracking().Where(Due(now))
            .OrderBy(j => j.RunAfter).ThenBy(j => j.Id).Select(j => j.Id).Take(10).ToListAsync(ct);

        foreach (var id in candidates)
        {
            var lease = now + _options.LeaseDuration;
            var claimed = await db.Set<BackgroundJob>().Where(j => j.Id == id).Where(Due(now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobStatus.Running)
                    .SetProperty(j => j.LockedBy, InstanceId)
                    .SetProperty(j => j.LockedUntil, lease)
                    .SetProperty(j => j.Attempts, j => j.Attempts + 1), ct);
            if (claimed == 1)
                return await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(j => j.Id == id, ct);
        }
        return null;
    }

    /// <summary>Claims and runs due jobs one after another. Used by tests and maintenance.</summary>
    public async Task<int> RunDueJobsAsync(int maxJobs, CancellationToken ct)
    {
        var ran = 0;
        while (ran < maxJobs && await TryClaimAsync(ct) is { } job)
        {
            await RunAsync(job, ct);
            ran++;
        }
        return ran;
    }

    private enum Outcome { Succeeded, Retry, Failed, Released }

    /// <summary>Runs a job this instance has claimed and records the outcome.</summary>
    public async Task RunAsync(BackgroundJob job, CancellationToken ct)
    {
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = RenewLeaseAsync(job.Id, heartbeatStop.Token);

        Outcome outcome;
        string? error = null;
        var sensitive = false;
        try
        {
            if (job.Attempts > job.MaxAttempts)
                throw new PermanentJobFailureException($"Gave up after {job.MaxAttempts} attempts.");

            await using var scope = _scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().FirstOrDefault(h => h.Kind == job.Kind)
                ?? throw new PermanentJobFailureException($"No handler is registered for job kind '{job.Kind}'.");
            sensitive = handler.PayloadIsSensitive;

            using (EnterScope(scope.ServiceProvider, job))
                await handler.HandleAsync(new BackgroundJobContext(job.Id, job.Kind, job.TenantId, job.PayloadJson, job.Attempts), ct);
            outcome = Outcome.Succeeded;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            outcome = Outcome.Released;
        }
        catch (PermanentJobFailureException ex)
        {
            outcome = Outcome.Failed;
            error = ex.Message;
        }
        catch (Exception ex)
        {
            error = Describe(ex);
            outcome = job.Attempts >= job.MaxAttempts ? Outcome.Failed : Outcome.Retry;
            if (outcome == Outcome.Retry)
                _logger.LogWarning(ex, "Background job {JobId} ({Kind}) failed on attempt {Attempt} of {Max}; retrying", job.Id, job.Kind, job.Attempts, job.MaxAttempts);
        }
        finally
        {
            heartbeatStop.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }

        await FinishAsync(job, outcome, error, sensitive);
    }

    private static IDisposable EnterScope(IServiceProvider services, BackgroundJob job) =>
        job.TenantId is long tenantId
            ? services.GetRequiredService<ITenantContext>().BeginTenantScope(tenantId)
            : services.GetRequiredService<INoTenantOperation>().BeginScope($"Background job {job.Kind} #{job.Id}", actorUserId: 0, actorRole: PrivilegedRoles.SystemJob);

    private async Task FinishAsync(BackgroundJob job, Outcome outcome, string? error, bool sensitive)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var mine = db.Set<BackgroundJob>().Where(j => j.Id == job.Id && j.LockedBy == InstanceId);
        var now = DateTime.UtcNow;
        var payload = sensitive ? RedactedPayload : job.PayloadJson;

        var updated = outcome switch
        {
            Outcome.Succeeded => await mine.ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Succeeded)
                .SetProperty(j => j.CompletedAt, now)
                .SetProperty(j => j.LastError, (string?)null)
                .SetProperty(j => j.PayloadJson, payload)
                .SetProperty(j => j.LockedBy, (string?)null)
                .SetProperty(j => j.LockedUntil, (DateTime?)null)),
            Outcome.Failed => await mine.ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Failed)
                .SetProperty(j => j.CompletedAt, now)
                .SetProperty(j => j.LastError, Truncate(error))
                .SetProperty(j => j.PayloadJson, payload)
                .SetProperty(j => j.LockedBy, (string?)null)
                .SetProperty(j => j.LockedUntil, (DateTime?)null)),
            Outcome.Retry => await mine.ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Pending)
                .SetProperty(j => j.RunAfter, now + Backoff(job.Attempts))
                .SetProperty(j => j.LastError, Truncate(error))
                .SetProperty(j => j.LockedBy, (string?)null)
                .SetProperty(j => j.LockedUntil, (DateTime?)null)),
            _ => await mine.ExecuteUpdateAsync(s => s
                // Shutting down: hand the job back without counting the interrupted attempt.
                .SetProperty(j => j.Status, JobStatus.Pending)
                .SetProperty(j => j.RunAfter, now)
                .SetProperty(j => j.Attempts, j => j.Attempts - 1)
                .SetProperty(j => j.LockedBy, (string?)null)
                .SetProperty(j => j.LockedUntil, (DateTime?)null)),
        };

        if (updated == 0)
            _logger.LogWarning("Background job {JobId} ({Kind}) finished, but this instance no longer held its lease; another instance may have run it", job.Id, job.Kind);
        else if (outcome == Outcome.Failed)
            _logger.LogError("Background job {JobId} ({Kind}) failed permanently after {Attempts} attempt(s): {Error}", job.Id, job.Kind, job.Attempts, error);
        else if (outcome == Outcome.Succeeded)
            _logger.LogInformation("Background job {JobId} ({Kind}) succeeded", job.Id, job.Kind);
    }

    private async Task RenewLeaseAsync(long jobId, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.LeaseDuration / 3);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var lease = DateTime.UtcNow + _options.LeaseDuration;
            var renewed = await db.Set<BackgroundJob>().Where(j => j.Id == jobId && j.LockedBy == InstanceId)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedUntil, lease), ct);
            if (renewed == 0)
                _logger.LogWarning("Lost the lease on background job {JobId}", jobId);
        }
    }

    /// <summary>30 s, 1 min, 2 min, ... up to an hour, with ±20% jitter so retries spread out.</summary>
    internal static TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Min(30 * Math.Pow(2, Math.Max(0, attempt - 1)), MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(seconds * (0.8 + Random.Shared.NextDouble() * 0.4));
    }

    internal static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";

    private static string? Truncate(string? s) => s is { Length: > 2000 } ? s[..2000] : s;
}

/// <summary>Runs queued jobs continuously, up to <see cref="JobOptions.MaxConcurrency"/> at a time.</summary>
public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly BackgroundJobRunner _runner;
    private readonly JobOptions _options;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(BackgroundJobRunner runner, IOptions<JobOptions> options, ILogger<BackgroundJobWorker> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Background job worker is disabled (Jobs:Enabled=false)");
            return;
        }

        using var slots = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
        var running = new List<Task>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await slots.WaitAsync(stoppingToken);
                BackgroundJob? job = null;
                try
                {
                    job = await _runner.TryClaimAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Could not claim background jobs");
                }

                if (job is null)
                {
                    slots.Release();
                    await Task.Delay(_options.PollInterval, stoppingToken);
                    continue;
                }

                running.RemoveAll(t => t.IsCompleted);
                running.Add(Task.Run(async () =>
                {
                    try { await _runner.RunAsync(job, stoppingToken); }
                    catch (Exception ex) { _logger.LogError(ex, "Background job {JobId} could not be completed", job.Id); }
                    finally { slots.Release(); }
                }, CancellationToken.None));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        await Task.WhenAll(running);
    }
}

/// <summary>
/// Runs <see cref="IScheduledJob"/>s when due. The next run time is stored per job and
/// claimed with a lease, so a restart does not rerun a daily job and only one instance runs it.
/// </summary>
public sealed class ScheduledJobRunner
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IEnumerable<IScheduledJob> _jobs;
    private readonly ILogger<ScheduledJobRunner> _logger;
    private readonly JobOptions _options;
    private readonly string _instanceId = $"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid():N}";

    public ScheduledJobRunner(IServiceScopeFactory scopes, IEnumerable<IScheduledJob> jobs, ILogger<ScheduledJobRunner> logger, IOptions<JobOptions> options)
    {
        _scopes = scopes;
        _jobs = jobs;
        _logger = logger;
        _options = options.Value;
    }

    public IReadOnlyList<string> JobNames => _jobs.Select(j => j.Name).ToList();

    /// <summary>Runs every job that is due and not running elsewhere. Returns the names that ran.</summary>
    public async Task<List<string>> RunDueAsync(CancellationToken ct)
    {
        var ran = new List<string>();
        foreach (var job in _jobs)
        {
            await EnsureStateAsync(job, ct);
            if (!await TryClaimAsync(job.Name, ct)) continue;
            ran.Add(job.Name);
            await RunClaimedAsync(job, ct);
        }
        return ran;
    }

    private async Task EnsureStateAsync(IScheduledJob job, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        if (await db.Set<ScheduledJobState>().AnyAsync(s => s.Name == job.Name, ct)) return;
        db.Set<ScheduledJobState>().Add(new ScheduledJobState { Name = job.Name, NextRunAt = DateTime.UtcNow + job.FirstRunDelay });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* another instance created it first */ }
    }

    private async Task<bool> TryClaimAsync(string name, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var now = DateTime.UtcNow;
        var lease = now + _options.LeaseDuration;
        return await db.Set<ScheduledJobState>()
            .Where(s => s.Name == name && s.NextRunAt <= now && (s.LockedUntil == null || s.LockedUntil < now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LockedBy, _instanceId)
                .SetProperty(x => x.LockedUntil, lease)
                .SetProperty(x => x.LastStartedAt, now), ct) == 1;
    }

    private async Task RunClaimedAsync(IScheduledJob job, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = RenewLeaseAsync(job.Name, heartbeatStop.Token);
        string result;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var noTenant = scope.ServiceProvider.GetRequiredService<INoTenantOperation>();
            using (noTenant.BeginScope($"Scheduled job: {job.Name}", actorUserId: 0, actorRole: PrivilegedRoles.SystemJob))
                await job.RunAsync(scope.ServiceProvider, ct);
            result = "ok";
            _logger.LogInformation("Scheduled job {Job} completed", job.Name);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            heartbeatStop.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
            await ReleaseAsync(job.Name, advance: null, started, "interrupted by shutdown");
            return;
        }
        catch (Exception ex)
        {
            result = BackgroundJobRunner.Describe(ex);
            _logger.LogError(ex, "Scheduled job {Job} failed", job.Name);
        }

        heartbeatStop.Cancel();
        try { await heartbeat; } catch (OperationCanceledException) { }
        await ReleaseAsync(job.Name, started + job.Interval, started, result);
    }

    private async Task ReleaseAsync(string name, DateTime? advance, DateTime started, string result)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var now = DateTime.UtcNow;
        var mine = db.Set<ScheduledJobState>().Where(s => s.Name == name && s.LockedBy == _instanceId);
        var text = result.Length > 2000 ? result[..2000] : result;
        if (advance is DateTime next)
            await mine.ExecuteUpdateAsync(s => s
                .SetProperty(x => x.NextRunAt, next)
                .SetProperty(x => x.LastFinishedAt, now)
                .SetProperty(x => x.LastResult, text)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTime?)null));
        else
            await mine.ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastResult, text)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTime?)null));
    }

    private async Task RenewLeaseAsync(string name, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.LeaseDuration / 3);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var lease = DateTime.UtcNow + _options.LeaseDuration;
            await db.Set<ScheduledJobState>().Where(s => s.Name == name && s.LockedBy == _instanceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockedUntil, lease), ct);
        }
    }
}

public sealed class ScheduledJobWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private readonly ScheduledJobRunner _runner;
    private readonly JobOptions _options;
    private readonly ILogger<ScheduledJobWorker> _logger;

    public ScheduledJobWorker(ScheduledJobRunner runner, IOptions<JobOptions> options, ILogger<ScheduledJobWorker> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        using var timer = new PeriodicTimer(CheckInterval);
        try
        {
            do
            {
                try { await _runner.RunDueAsync(stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Checking scheduled jobs failed"); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

/// <summary>Deletes finished jobs: succeeded after 14 days, failed after 90.</summary>
public sealed class JobCleanupScheduledJob : IScheduledJob
{
    public string Name => "background-job-cleanup";
    public TimeSpan Interval => TimeSpan.FromDays(1);
    public TimeSpan FirstRunDelay => TimeSpan.FromMinutes(10);

    public async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<LearnCloudDbContext>();
        var now = DateTime.UtcNow;
        var succeededBefore = now.AddDays(-14);
        var failedBefore = now.AddDays(-90);
        await db.Set<BackgroundJob>().Where(j => j.Status == JobStatus.Succeeded && j.CompletedAt < succeededBefore).ExecuteDeleteAsync(ct);
        await db.Set<BackgroundJob>().Where(j => j.Status == JobStatus.Failed && j.CompletedAt < failedBefore).ExecuteDeleteAsync(ct);
    }
}
