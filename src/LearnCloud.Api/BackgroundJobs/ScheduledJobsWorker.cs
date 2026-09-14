using LearnCloud.Communication.Services;

using LearnCloud.MultiTenancy.Security;
using LearnCloud.PlatformBilling.Jobs;

namespace LearnCloud.Api.BackgroundJobs;

// Runs the platform's recurring cross-tenant jobs. Neither job was scheduled by anything
// before: DunningJob and the communication rule engine existed but never ran.
//
// Each run gets a fresh DI scope and an explicit, audited no-tenant scope under the
// SYSTEM_JOB role. Jobs that touch tenant data narrow to one tenant at a time inside.
// Disable with Jobs:Enabled=false (local development and tests do).
public sealed class ScheduledJobsWorker : BackgroundService
{
    private static readonly TimeSpan RulesInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan DunningInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledJobsWorker> _logger;

    public ScheduledJobsWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduledJobsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextRules = DateTime.UtcNow.AddMinutes(1);
        var nextDunning = DateTime.UtcNow.AddMinutes(5);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTime.UtcNow;
            if (now >= nextRules)
            {
                await RunAsync("communication rules", (sp, ct) => sp.GetRequiredService<ICommunicationRuleEngine>().ProcessScheduledRulesAsync(ct), stoppingToken);
                nextRules = now.Add(RulesInterval);
            }
            if (now >= nextDunning)
            {
                await RunAsync("billing dunning", (sp, ct) => sp.GetRequiredService<DunningJob>().RunAsync(ct), stoppingToken);
                nextDunning = now.Add(DunningInterval);
            }
        }
    }

    private async Task RunAsync(string name, Func<IServiceProvider, CancellationToken, Task> job, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            // Through INoTenantOperation rather than the tenant context directly, so the
            // bypass is logged critically and recorded in the audit log like any other.
            var noTenant = scope.ServiceProvider.GetRequiredService<INoTenantOperation>();
            using (noTenant.BeginScope($"Scheduled job: {name}", actorUserId: 0, actorRole: PrivilegedRoles.SystemJob))
            {
                await job(scope.ServiceProvider, ct);
            }
            _logger.LogInformation("Scheduled job {Job} completed", name);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled job {Job} failed", name);
        }
    }
}
