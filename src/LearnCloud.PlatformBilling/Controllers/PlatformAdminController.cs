using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformBilling.Controllers;

// Platform admin views: tenant list with subscription state, revenue by month, trials converting, tenants at risk, manual override

[ApiController]
[Route("api/platform")]
[EnableRateLimiting("api_general")]
[Authorize(Roles = "PLATFORM_SUPERADMIN")]
public class PlatformAdminController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PlatformAdminController(LearnCloudDbContext db, ITenantContext tenantContext) { _db = db; _tenantContext = tenantContext; }

    private long ActorUserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Tenant list with subscription state
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants([FromQuery] string? state, CancellationToken ct)
    {
        var query = _db.Set<Subscription>().Include(s => s.Plan).Include(s => s.Tenant).Where(s => !s.IsDeleted).AsQueryable();
        if (!string.IsNullOrEmpty(state) && Enum.TryParse<SubscriptionState>(state, true, out var st))
            query = query.Where(s => s.State == st);

        var list = await query.OrderByDescending(s => s.CreatedAt).Take(200).Select(s => new
        {
            tenantId = s.TenantId,
            tenantName = s.Tenant.Name,
            slug = s.Tenant.Slug,
            city = s.Tenant.City,
            plan = s.Plan.Name,
            planCode = s.Plan.Code,
            state = s.State.ToString(),
            trialEndsAt = s.TrialEndsAt,
            currentPeriodStart = s.CurrentPeriodStart,
            currentPeriodEnd = s.CurrentPeriodEnd,
            billableLearnerCount = s.BillableLearnerCount,
            currentLearnerCount = s.CurrentLearnerCount,
            pastDueSince = s.PastDueSince,
            suspendedSince = s.SuspendedSince,
            readOnlyUntil = s.ReadOnlyUntil,
            learnerLimit = s.Plan.LearnerLimit,
            pricePerLearner = s.Plan.PricePerLearnerPerTerm,
            isOverLimit = s.CurrentLearnerCount > s.Plan.LearnerLimit
        }).ToListAsync(ct);

        return Ok(list);
    }

    // Revenue by month
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("revenue/by-month")]
    public async Task<IActionResult> RevenueByMonth([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var invoices = await _db.Set<PlatformInvoice>().Where(i => !i.IsDeleted && i.IssueDate.Year == y && i.Status == "paid").ToListAsync(ct);
        var payments = await _db.Set<PlatformPayment>().Where(p => !p.IsDeleted && p.PaymentDate.Year == y).ToListAsync(ct);

        var byMonth = Enumerable.Range(1, 12).Select(m => new
        {
            month = m,
            monthName = new DateTime(y, m, 1).ToString("MMM"),
            revenue = payments.Where(p => p.PaymentDate.Month == m).Sum(p => p.Amount),
            invoicesIssued = invoices.Count(i => i.IssueDate.Month == m),
            invoicesPaid = payments.Count(p => p.PaymentDate.Month == m),
            mrr = payments.Where(p => p.PaymentDate.Month == m).Sum(p => p.Amount) // simplified MRR
        }).ToList();

        var totalRevenue = byMonth.Sum(b => b.revenue);

        return Ok(new { year = y, totalRevenue, byMonth });
    }

    // Trials converting
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("trials/converting")]
    public async Task<IActionResult> TrialsConverting(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var trialing = await _db.Set<Subscription>().Where(s => s.State == SubscriptionState.Trialing && !s.IsDeleted).Include(s => s.Tenant).Include(s => s.Plan).ToListAsync(ct);

        var trials = trialing.Select(s => new
        {
            tenantId = s.TenantId,
            tenantName = s.Tenant.Name,
            slug = s.Tenant.Slug,
            trialStartedAt = s.TrialStartedAt,
            trialEndsAt = s.TrialEndsAt,
            daysLeft = s.TrialEndsAt.HasValue ? (s.TrialEndsAt.Value - now).Days : 0,
            daysSinceStart = s.TrialStartedAt.HasValue ? (now - s.TrialStartedAt.Value).Days : 0,
            isAtRisk = s.TrialEndsAt.HasValue && (s.TrialEndsAt.Value - now).Days <= 2,
            plan = s.Plan.Name
        }).ToList();

        var converting = trials.Count(t => t.daysSinceStart >= 7); // engaged trials
        var atRisk = trials.Count(t => t.isAtRisk);

        return Ok(new { totalTrials = trials.Count, converting, atRisk, trials });
    }

    // Tenants at risk: past_due, suspended, trial ending soon, over learner limit
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("tenants/at-risk")]
    public async Task<IActionResult> TenantsAtRisk(CancellationToken ct)
    {
        var atRisk = await _db.Set<Subscription>().Where(s => !s.IsDeleted && (s.State == SubscriptionState.PastDue || s.State == SubscriptionState.Suspended || s.State == SubscriptionState.Expired || s.CurrentLearnerCount > s.Plan.LearnerLimit)).Include(s => s.Tenant).Include(s => s.Plan).ToListAsync(ct);

        var list = atRisk.Select(s => new
        {
            tenantId = s.TenantId,
            tenantName = s.Tenant.Name,
            slug = s.Tenant.Slug,
            state = s.State.ToString(),
            plan = s.Plan.Name,
            billable = s.BillableLearnerCount,
            current = s.CurrentLearnerCount,
            limit = s.Plan.LearnerLimit,
            overLimit = s.CurrentLearnerCount > s.Plan.LearnerLimit,
            pastDueSince = s.PastDueSince,
            suspendedSince = s.SuspendedSince,
            trialEndsAt = s.TrialEndsAt,
            reason = s.State == SubscriptionState.PastDue ? $"Past due since {s.PastDueSince:yyyy-MM-dd}" : s.State == SubscriptionState.Suspended ? $"Suspended since {s.SuspendedSince:yyyy-MM-dd}" : s.CurrentLearnerCount > s.Plan.LearnerLimit ? $"Over learner limit {s.CurrentLearnerCount}/{s.Plan.LearnerLimit}" : $"Trial ends {s.TrialEndsAt:yyyy-MM-dd}"
        }).ToList();

        return Ok(new { count = list.Count, tenants = list });
    }

    // Manual override to extend trial or credit invoice, fully audited
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("overrides/extend-trial")]
    public async Task<IActionResult> ExtendTrial([FromBody] ExtendTrialRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            return BadRequest(new { message = "Reason >=10 chars required for audit" });

        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == req.TenantId && !s.IsDeleted, ct);
        if (sub == null) return NotFound();

        var oldTrialEnds = sub.TrialEndsAt;
        sub.TrialEndsAt = req.NewTrialEndsAt;
        // If was expired, move back to trialing via manual override
        if (sub.State == SubscriptionState.Expired)
        {
            sub.State = SubscriptionState.Trialing;
            sub.ReadOnlyUntil = null;
            sub.ExpiredSince = null;
        }

        var overrideEntry = new BillingOverride
        {
            TenantId = req.TenantId,
            SubscriptionId = sub.Id,
            OverrideType = "extend_trial",
            DetailsJson = $"{{\"oldTrialEndsAt\":\"{oldTrialEnds}\",\"newTrialEndsAt\":\"{req.NewTrialEndsAt:o}\"}}",
            Reason = req.Reason,
            AdminUserId = ActorUserId
        };
        _db.Set<BillingOverride>().Add(overrideEntry);

        _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
        {
            TenantId = req.TenantId,
            UserId = ActorUserId,
            EntityType = "Subscription",
            EntityId = sub.Id,
            Action = "manual_override_extend_trial",
            OldValues = $"{{\"oldTrialEndsAt\":\"{oldTrialEnds}\"}}",
            NewValues = $"{{\"newTrialEndsAt\":\"{req.NewTrialEndsAt:o}\",\"reason\":\"{req.Reason}\"}}",
            CreatedBy = ActorUserId
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new { message = $"Trial extended from {oldTrialEnds} to {req.NewTrialEndsAt}", subscription = sub, overrideId = overrideEntry.Id });
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("overrides/credit-invoice")]
    public async Task<IActionResult> CreditInvoice([FromBody] CreditInvoiceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            return BadRequest(new { message = "Reason >=10 chars required" });

        var invoice = await _db.Set<PlatformInvoice>().FirstOrDefaultAsync(i => i.Id == req.InvoiceId && i.TenantId == req.TenantId && !i.IsDeleted, ct);
        if (invoice == null) return NotFound();

        if (req.Amount <=0 || req.Amount > invoice.BalanceDue)
            return BadRequest(new { message = $"Credit amount must be >0 and <= balance {invoice.BalanceDue}" });

        invoice.DiscountAmount = Math.Round(invoice.DiscountAmount + req.Amount, 2, MidpointRounding.AwayFromZero);
        invoice.BalanceDue = Math.Round(invoice.BalanceDue - req.Amount, 2, MidpointRounding.AwayFromZero);
        if (invoice.BalanceDue <=0)
        {
            invoice.BalanceDue = 0;
            invoice.Status = "paid";
        }

        var overrideEntry = new BillingOverride
        {
            TenantId = req.TenantId,
            InvoiceId = invoice.Id,
            SubscriptionId = invoice.SubscriptionId,
            OverrideType = "credit_invoice",
            DetailsJson = $"{{\"amount\":{req.Amount},\"currency\":\"{invoice.Currency}\"}}",
            Reason = req.Reason,
            AdminUserId = ActorUserId
        };
        _db.Set<BillingOverride>().Add(overrideEntry);

        _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
        {
            TenantId = req.TenantId,
            UserId = ActorUserId,
            EntityType = "PlatformInvoice",
            EntityId = invoice.Id,
            Action = "manual_override_credit_invoice",
            OldValues = $"{{\"balanceDue\":{invoice.BalanceDue + req.Amount}}}",
            NewValues = $"{{\"balanceDue\":{invoice.BalanceDue},\"creditAmount\":{req.Amount},\"reason\":\"{req.Reason}\"}}",
            CreatedBy = ActorUserId
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new { message = $"Invoice {invoice.InvoiceNumber} credited {req.Amount} {invoice.Currency}, new balance {invoice.BalanceDue}", invoice, overrideId = overrideEntry.Id });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("overrides")]
    public async Task<IActionResult> ListOverrides([FromQuery] long? tenantId, CancellationToken ct)
    {
        var q = _db.Set<BillingOverride>().Where(o => !o.IsDeleted).AsQueryable();
        if (tenantId.HasValue) q = q.Where(o => o.TenantId == tenantId.Value);
        var list = await q.OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    // Revenue MRR etc for admin console
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("revenue/summary")]
    public async Task<IActionResult> RevenueSummary(CancellationToken ct)
    {
        var totalTenants = await _db.Tenants.CountAsync(t => !t.IsDeleted, ct);
        var activeSubs = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.Active && !s.IsDeleted, ct);
        var trialing = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.Trialing && !s.IsDeleted, ct);
        var pastDue = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.PastDue && !s.IsDeleted, ct);
        var suspended = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.Suspended && !s.IsDeleted, ct);

        var paymentsThisMonth = await _db.Set<PlatformPayment>().Where(p => !p.IsDeleted && p.PaymentDate.Month == DateTime.UtcNow.Month && p.PaymentDate.Year == DateTime.UtcNow.Year).SumAsync(p => p.Amount, ct);
        var outstanding = await _db.Set<PlatformInvoice>().Where(i => !i.IsDeleted && i.Status != "paid" && i.Status != "void").SumAsync(i => i.BalanceDue, ct);

        return Ok(new
        {
            totalTenants,
            active = activeSubs,
            trialing,
            pastDue,
            suspended,
            mrr = paymentsThisMonth,
            outstanding,
            churnRisk = pastDue + suspended
        });
    }
}

public record ExtendTrialRequest(long TenantId, DateTime NewTrialEndsAt, string Reason);
public record CreditInvoiceRequest(long TenantId, long InvoiceId, decimal Amount, string Reason);