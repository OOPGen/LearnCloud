using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformBilling.Controllers;

// How schools pay you - tenant facing billing

[ApiController]
[Route("api/billing")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class BillingController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IBillingService _billingService;
    private readonly BillingOptions _billing;

    public BillingController(LearnCloudDbContext db, ITenantContext tenantContext, IBillingService billingService, IOptions<BillingOptions> billing)
    {
        _db = db; _tenantContext = tenantContext; _billingService = billingService; _billing = billing.Value;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Get current subscription with state, trial reminders, read-only banner info
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var sub = await _db.Set<Subscription>().Include(s=>s.Plan).FirstOrDefaultAsync(s=>s.TenantId==TenantId && !s.IsDeleted, ct);
        if (sub == null) return NotFound(new { message = "No subscription found" });

        // Read-only status comes from the same rule the API enforces. This used to build a
        // payment link from sub.Tenant, which was never loaded, so every call failed with 500.
        var isReadOnly = _billing.EnforceReadOnly && SubscriptionAccess.IsReadOnly(sub.State);
        var banner = SubscriptionAccess.Banner(sub, _billing.ContactEmail);

        return Ok(new
        {
            subscription = new
            {
                sub.Id,
                sub.TenantId,
                plan = sub.Plan.Name,
                planCode = sub.Plan.Code,
                state = sub.State.ToString(),
                sub.TrialStartedAt,
                sub.TrialEndsAt,
                sub.CurrentPeriodStart,
                sub.CurrentPeriodEnd,
                sub.BillableLearnerCount,
                sub.CurrentLearnerCount,
                learnerLimit = sub.Plan.LearnerLimit,
                pricePerLearner = sub.Plan.PricePerLearnerPerTerm,
                minimumCharge = sub.Plan.MinimumCharge,
                includedSms = sub.Plan.IncludedSmsBundle,
                includedModules = sub.Plan.IncludedModulesJson,
                sub.PastDueSince,
                sub.SuspendedSince,
                sub.ReadOnlyUntil,
                sub.PendingPlanId,
                sub.PendingPlanEffectiveAt
            },
            isReadOnly,
            banner = string.IsNullOrEmpty(banner) ? null : banner,
            contactEmail = _billing.ContactEmail,
            canEdit = !isReadOnly
        });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(CancellationToken ct)
    {
        var invoices = await _billingService.GetInvoicesAsync(TenantId, ct);
        return Ok(invoices);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("invoices/{id:long}")]
    public async Task<IActionResult> GetInvoice(long id, CancellationToken ct)
    {
        var inv = await _db.Set<PlatformInvoice>().Include(i=>i.Lines).FirstOrDefaultAsync(i=>i.Id==id && i.TenantId==TenantId && !i.IsDeleted, ct);
        if (inv == null) return NotFound();
        return Ok(inv);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("plan/change")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest req, CancellationToken ct)
    {
        var service = HttpContext.RequestServices.GetService(typeof(SubscriptionService)) as SubscriptionService;
        if (service == null) return StatusCode(500);

        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s=>s.TenantId==TenantId && !s.IsDeleted, ct);
        if (sub == null) return NotFound();

        var isUpgrade = false;
        var currentPlan = await _db.Set<Plan>().FirstOrDefaultAsync(p=>p.Id==sub.PlanId, ct);
        var newPlan = await _db.Set<Plan>().FirstOrDefaultAsync(p=>p.Id==req.NewPlanId, ct);
        if (newPlan == null) return BadRequest(new { message = "New plan not found" });

        // Determine upgrade vs downgrade by learner limit or price
        if (newPlan.LearnerLimit > currentPlan!.LearnerLimit || newPlan.PricePerLearnerPerTerm > currentPlan.PricePerLearnerPerTerm)
            isUpgrade = true;

        var result = await service.ChangePlanAsync(TenantId, req.NewPlanId, UserId, isUpgrade, req.Reason, ct);

        return Ok(new
        {
            message = isUpgrade ? $"Upgrade to {newPlan.Name} takes effect immediately with pro-rata charge" : $"Downgrade to {newPlan.Name} takes effect at next period {result.CurrentPeriodEnd:yyyy-MM-dd}",
            subscription = result,
            isUpgrade,
            effective = isUpgrade ? DateTime.UtcNow : result.CurrentPeriodEnd
        });
    }

    // Tenant read-only banner check - used by frontend shell to show banner
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    // For every signed-in role of the school, not only admins. It used to allow anonymous
    // callers and take any tenantId from the query string, disclosing any school's billing state.
    [HttpGet("read-only-status")]
    public async Task<IActionResult> GetReadOnlyStatus(CancellationToken ct)
    {
        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s=>s.TenantId==TenantId && !s.IsDeleted, ct);
        if (sub == null) return Ok(new { isReadOnly = false, state = (string?)null, banner = (string?)null });

        var isReadOnly = _billing.EnforceReadOnly && SubscriptionAccess.IsReadOnly(sub.State);
        var banner = SubscriptionAccess.Banner(sub, _billing.ContactEmail);
        return Ok(new
        {
            isReadOnly,
            state = sub.State.ToString(),
            banner = string.IsNullOrEmpty(banner) ? null : banner,
            contactEmail = _billing.ContactEmail,
            readOnlyUntil = sub.ReadOnlyUntil
        });
    }
}

public record ChangePlanRequest(long NewPlanId, string? Reason);