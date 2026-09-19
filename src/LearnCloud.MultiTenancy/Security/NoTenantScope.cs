using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LearnCloud.MultiTenancy.Security;

// 8. Mechanism for genuinely tenant-less operations (platform admin, background jobs) that is explicit, requires privileged role, and is audited

public interface INoTenantOperation
{
    Task<T> ExecuteAsync<T>(string reason, long actorUserId, string actorRole, Func<Task<T>> operation);
    Task ExecuteAsync(string reason, long actorUserId, string actorRole, Func<Task> operation);
    IDisposable BeginScope(string reason, long actorUserId, string actorRole);
}

public class NoTenantOperation : INoTenantOperation
{
    private readonly ITenantContext _tenantContext;
    private readonly LearnCloudDbContext _db;
    private readonly ILogger<NoTenantOperation> _logger;


    public NoTenantOperation(ITenantContext tenantContext, LearnCloudDbContext db, ILogger<NoTenantOperation> logger)
    {
        _tenantContext = tenantContext;
        _db = db;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(string reason, long actorUserId, string actorRole, Func<Task<T>> operation)
    {
        ValidateRole(actorRole);
        using var scope = BeginScope(reason, actorUserId, actorRole);
        var result = await operation();
        return result;
    }

    public async Task ExecuteAsync(string reason, long actorUserId, string actorRole, Func<Task> operation)
    {
        ValidateRole(actorRole);
        using var scope = BeginScope(reason, actorUserId, actorRole);
        await operation();
    }

    public IDisposable BeginScope(string reason, long actorUserId, string actorRole)
    {
        ValidateRole(actorRole);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new ArgumentException("No-tenant operation requires reason >=10 chars for audit");

        // Scheduled and background jobs open these scopes all the time; logging each one as
        // Critical (with a stack trace) buried real alerts. Jobs log at Information, people
        // (platform staff) at Warning. Every scope is still written to the audit log below.
        if (actorRole == PrivilegedRoles.SystemJob)
            _logger.LogInformation("No-tenant scope for system job: {Reason}", reason);
        else
            _logger.LogWarning("EXPLICIT NO-TENANT SCOPE: Reason={Reason} Actor={ActorUserId} Role={Role} Stack={Stack}",
                reason, actorUserId, actorRole, Environment.StackTrace);

        // Audit log the explicit scope creation
        var audit = new AuditLog
        {
            TenantId = null, // platform
            UserId = actorUserId,
            EntityType = "TenantContext",
            EntityId = 0,
            Action = "explicit_no_tenant_begin",
            NewValues = $"{{\"reason\":\"{reason}\",\"role\":\"{actorRole}\"}}",
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorUserId
        };
        // Fire and forget? For demo we add synchronously but don't SaveChanges yet - will be saved with operation's SaveChanges
        _db.AuditLogs.Add(audit);
        // Note: we don't call SaveChanges here, caller will, but we have logged intent

        return _tenantContext.BeginNoTenantScope(reason, actorUserId, actorRole);
    }

    private void ValidateRole(string role)
    {
        if (!PrivilegedRoles.CanUseNoTenantScope(role))
        {
            _logger.LogCritical("Unauthorized attempt to begin no-tenant scope with role {Role}", role);
            throw new UnauthorizedAccessException($"Role {role} not allowed for explicit no-tenant operations. Allowed: {string.Join(',', PrivilegedRoles.All)}");
        }
    }
}

// Example usage in background job:
/*
public class NightlyBillingJob
{
    private readonly INoTenantOperation _noTenant;
    public NightlyBillingJob(INoTenantOperation noTenant) => _noTenant = noTenant;

    public async Task RunAsync()
    {
        await _noTenant.ExecuteAsync("Nightly billing - meter active students for all tenants", actorUserId: 1, actorRole: "SYSTEM_JOB", async () =>
        {
            // Inside here, _tenantContext.IsExplicitNoTenant = true, global query filter bypassed via IsExplicitNoTenant flag
            // You must still query with IgnoreQueryFilters() or our filter allows all when IsExplicitNoTenant true
            var tenants = await db.Tenants.Where(t=>!t.IsDeleted).ToListAsync();
            foreach(var tenant in tenants)
            {
                using var tenantScope = _tenantContext.BeginTenantScope(tenant.Id);
                var activeCount = await db.Students.CountAsync(s=>s.Status=="active"); // filtered by tenant now
                // update subscription etc
            }
        });
    }
}
*/

// Platform admin example:
/*
[RequiresPermission("platform.tenants.read")]
public async Task<IActionResult> ListAllTenants()
{
    // Explicit no-tenant required
    using var scope = _noTenantOperation.BeginScope("Platform admin listing all tenants for dashboard", User.GetUserId(), "PLATFORM_SUPERADMIN");
    var tenants = await db.Tenants.IgnoreQueryFilters().Where(t=>!t.IsDeleted).ToListAsync();
    return Ok(tenants);
}
*/
