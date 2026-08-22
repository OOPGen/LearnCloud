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

    public BillingController(LearnCloudDbContext db, ITenantContext tenantContext, IBillingService billingService)
    {
        _db = db; _tenantContext = tenantContext; _billingService = billingService;
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

        var isReadOnly = sub.State == SubscriptionState.Suspended || sub.State == SubscriptionState.Expired || sub.State == SubscriptionState.PastDue;
        var banner = "";
        var paymentLink = $"https://{sub.Tenant.Slug}.learncloud.co.zw/billing/pay";

        if (sub.State == SubscriptionState.Suspended)
            banner = $"Your account is suspended due to non-payment since {sub.SuspendedSince:yyyy-MM-dd}. You are in read-only mode with a clear banner and payment link. You can view and export your records, but cannot edit. Pay now to reactivate. Never delete data, never lock out entirely. Payment link: {paymentLink}";
        else if (sub.State == SubscriptionState.Expired)
            banner = $"Your trial expired on {sub.TrialEndsAt:yyyy-MM-dd}. You are in 30-day read-only window until {sub.ReadOnlyUntil:yyyy-MM-dd} before archival. Pay to keep data. Link: {paymentLink}";
        else if (sub.State == SubscriptionState.PastDue)
            banner = $"Your subscription is past due since {sub.PastDueSince:yyyy-MM-dd}. Please pay invoice to avoid suspension in {sub.PastDueGraceDays} days. Link: {paymentLink}";
        else if (sub.State == SubscriptionState.Trialing)
        {
            var daysLeft = sub.TrialEndsAt.HasValue ? (sub.TrialEndsAt.Value - DateTime.UtcNow).Days : 0;
            banner = $"Trial: {daysLeft} days left, no card required. Reminders at day 7, 12 and expiry. After expiry 30-day read-only window before archival.";
        }

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
            banner = isReadOnly || sub.State==SubscriptionState.Trialing ? banner : null,
            paymentLink = isReadOnly ? paymentLink : null,
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
    [HttpGet("read-only-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReadOnlyStatus([FromQuery] long? tenantId, CancellationToken ct)
    {
        var tid = tenantId ?? TenantId;
        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s=>s.TenantId==tid && !s.IsDeleted, ct);
        if (sub == null) return Ok(new { isReadOnly = false });

        var isReadOnly = sub.State == SubscriptionState.Suspended || sub.State == SubscriptionState.Expired || sub.State == SubscriptionState.PastDue;
        return Ok(new
        {
            isReadOnly,
            state = sub.State.ToString(),
            banner = isReadOnly ? $"Account {sub.State} - read-only with payment link" : null,
            paymentLink = isReadOnly ? $"/billing/pay?tenant={tid}" : null,
            readOnlyUntil = sub.ReadOnlyUntil
        });
    }
}

public record ChangePlanRequest(long NewPlanId, string? Reason);