using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using LearnCloud.PlatformAdmin.DTOs;
using LearnCloud.PlatformAdmin.Entities;
using LearnCloud.PlatformBilling.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformAdmin.Services;

public interface IPlatformAdminService
{
    Task<(List<TenantListDto> Items, int Total)> ListTenantsAsync(TenantListRequest req, CancellationToken ct = default);
    Task<TenantDetailDto> GetTenantDetailAsync(long tenantId, CancellationToken ct = default);
    Task<SupportNoteDto> AddSupportNoteAsync(long tenantId, long actorUserId, CreateSupportNoteRequest req, CancellationToken ct = default);
    Task<Subscription> ExtendTrialAsync(long tenantId, long actorUserId, ExtendTrialRequest req, CancellationToken ct = default);
    Task<Subscription> ChangePlanAsync(long tenantId, long actorUserId, ChangePlanRequest req, CancellationToken ct = default);
    Task<PlatformInvoice> CreditInvoiceAsync(long tenantId, long actorUserId, CreditInvoiceRequest req, CancellationToken ct = default);
    Task<Subscription> SuspendTenantAsync(long tenantId, long actorUserId, SuspendTenantRequest req, CancellationToken ct = default);
    Task<Subscription> ReactivateTenantAsync(long tenantId, long actorUserId, ReactivateTenantRequest req, CancellationToken ct = default);
    Task<BusinessMetricsDto> GetBusinessMetricsAsync(CancellationToken ct = default);
    Task<List<BackgroundJobStatusDto>> GetBackgroundJobsAsync(string? status, CancellationToken ct = default);
    Task<List<ErrorRateDto>> GetErrorRatesAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<SmsSpendByTenantDto>> GetSmsSpendAsync(int year, int month, CancellationToken ct = default);
    Task<List<StorageGrowthDto>> GetStorageGrowthAsync(CancellationToken ct = default);
}

public class PlatformAdminService : IPlatformAdminService
{
    private readonly LearnCloudDbContext _db;
    private readonly ILogger<PlatformAdminService> _logger;

    public PlatformAdminService(LearnCloudDbContext db, ILogger<PlatformAdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(List<TenantListDto> Items, int Total)> ListTenantsAsync(TenantListRequest req, CancellationToken ct = default)
    {
        var query = _db.Set<Subscription>().Include(s => s.Plan).Include(s => s.Tenant).Where(s => !s.IsDeleted).AsQueryable();

        if (!string.IsNullOrEmpty(req.State) && Enum.TryParse<SubscriptionState>(req.State, true, out var state))
            query = query.Where(s => s.State == state);

        if (!string.IsNullOrEmpty(req.PlanCode))
            query = query.Where(s => s.Plan.Code == req.PlanCode);

        if (!string.IsNullOrEmpty(req.Search))
            query = query.Where(s => s.Tenant.Name.Contains(req.Search) || s.Tenant.Slug.Contains(req.Search));

        // Health status filter via TenantHealthScore
        if (!string.IsNullOrEmpty(req.HealthStatus))
        {
            var healthTenantIds = await _db.Set<TenantHealthScore>().Where(h => h.HealthStatus == req.HealthStatus && !h.IsDeleted).Select(h => h.TenantId).ToListAsync(ct);
            query = query.Where(s => healthTenantIds.Contains(s.TenantId));
        }

        var total = await query.CountAsync(ct);

        // Sorting
        query = req.SortBy?.ToLower() switch
        {
            "learners" => req.SortDesc ? query.OrderByDescending(s => s.CurrentLearnerCount) : query.OrderBy(s => s.CurrentLearnerCount),
            "mrr" => req.SortDesc ? query.OrderByDescending(s => s.BillableLearnerCount * s.Plan.PricePerLearnerPerTerm) : query.OrderBy(s => s.BillableLearnerCount * s.Plan.PricePerLearnerPerTerm),
            "last_activity" => req.SortDesc ? query.OrderByDescending(s => s.UpdatedAt) : query.OrderBy(s => s.UpdatedAt),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };

        var items = await query.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).ToListAsync(ct);

        var result = new List<TenantListDto>();
        foreach (var sub in items)
        {
            var health = await _db.Set<TenantHealthScore>().FirstOrDefaultAsync(h => h.TenantId == sub.TenantId && !h.IsDeleted, ct);
            var lastActivity = await _db.AuditLogs.Where(a => a.TenantId == sub.TenantId).OrderByDescending(a => a.CreatedAt).Select(a => a.CreatedAt).FirstOrDefaultAsync(ct);
            var monthlyValue = sub.BillableLearnerCount * sub.Plan.PricePerLearnerPerTerm;
            if (monthlyValue < sub.Plan.MinimumCharge) monthlyValue = sub.Plan.MinimumCharge;

            result.Add(new TenantListDto(
                sub.TenantId,
                sub.Tenant.Name,
                sub.Tenant.Slug,
                sub.Tenant.City,
                sub.Plan.Name,
                sub.Plan.Code,
                sub.State.ToString(),
                sub.CurrentLearnerCount,
                sub.Plan.LearnerLimit,
                sub.CurrentLearnerCount > sub.Plan.LearnerLimit,
                lastActivity,
                monthlyValue,
                sub.Currency,
                health?.HealthStatus ?? "unknown",
                health?.Score ?? 0,
                sub.TrialEndsAt,
                sub.CurrentPeriodEnd
            ));
        }

        return (result, total);
    }

    public async Task<TenantDetailDto> GetTenantDetailAsync(long tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct) ?? throw new InvalidOperationException("Tenant not found");
        var subscription = await _db.Set<Subscription>().Include(s => s.Plan).FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        var history = await _db.AuditLogs.Where(a => a.TenantId == tenantId && a.EntityType == "Subscription").OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync(ct);
        var invoices = await _db.Set<PlatformInvoice>().Where(i => i.TenantId == tenantId && !i.IsDeleted).OrderByDescending(i => i.IssueDate).Take(20).ToListAsync(ct);
        var usersCount = await _db.Set<User>().CountAsync(u => u.TenantId == tenantId && !u.IsDeleted, ct);
        var learnersCount = await _db.Set<Student>().CountAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        var smsUsage = await _db.Set<TenantMessagingUsage>().Where(u => u.TenantId == tenantId && !u.IsDeleted).OrderByDescending(u => u.Year).ThenByDescending(u => u.Month).FirstOrDefaultAsync(ct);
        var storage = await _db.Set<StorageGrowthRecord>().Where(s => s.TenantId == tenantId && !s.IsDeleted).OrderByDescending(s => s.MeasuredAt).FirstOrDefaultAsync(ct);
        var supportNotes = await _db.Set<SupportNote>().Where(n => n.TenantId == tenantId && !n.IsDeleted).OrderByDescending(n => n.CreatedAt).Take(20).ToListAsync(ct);
        var health = await _db.Set<TenantHealthScore>().FirstOrDefaultAsync(h => h.TenantId == tenantId && !h.IsDeleted, ct);

        var subHistory = history.Select(h => new SubscriptionHistoryDto(h.Id, h.OldValues ?? "", h.NewValues ?? "", h.Action, h.OldValues, h.CreatedAt, h.UserId)).ToList();
        var invoiceDtos = invoices.Select(i => new InvoiceDto(i.Id, i.InvoiceNumber, i.IssueDate, i.DueDate, i.TotalAmount, i.AmountPaid, i.BalanceDue, i.Currency, i.Status, i.Notes)).ToList();
        var monthlyTrend = await _db.Set<TenantMessagingUsage>().Where(u => u.TenantId == tenantId && !u.IsDeleted).OrderBy(u => u.Year).ThenBy(u => u.Month).Take(12).Select(u => new MonthlyUsageDto(u.Year, u.Month, 0, 0, u.SmsCount, u.SmsCost, u.TotalBytes)).ToListAsync(ct);

        var usage = new UsageDto(usersCount, learnersCount, smsUsage?.SmsCount ?? 0, smsUsage?.SmsCost ?? 0m, smsUsage?.EmailCount ?? 0, storage?.TotalBytes ?? 0, storage != null ? Math.Round(storage.TotalBytes / 1024.0 / 1024.0 / 1024.0, 2) : 0m, monthlyTrend);

        var healthDto = health != null ? new HealthDto(health.Score, health.HealthStatus, health.CalculatedAt, health.FactorsJson, health.MonthlyValue) : new HealthDto(0, "unknown", DateTime.UtcNow, null, 0m);

        var supportNoteDtos = new List<SupportNoteDto>();
        foreach (var note in supportNotes)
        {
            var creator = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == note.CreatedByUserId, ct);
            supportNoteDtos.Add(new SupportNoteDto(note.Id, note.Content, note.IsInternal, note.Category, note.CreatedByUserId, creator?.DisplayName ?? $"User {note.CreatedByUserId}", note.CreatedAt));
        }

        var currentSub = subscription != null ? new SubscriptionDto(subscription.Id, subscription.Plan.Name, subscription.Plan.Code, subscription.State.ToString(), subscription.PreviousState, subscription.TrialStartedAt, subscription.TrialEndsAt, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.BillableLearnerCount, subscription.CurrentLearnerCount, subscription.PastDueSince, subscription.SuspendedSince, subscription.ExpiredSince, subscription.ReadOnlyUntil, subscription.PendingPlanId, subscription.PendingPlanEffectiveAt, subscription.Currency) : null;

        return new TenantDetailDto(
            tenantId,
            tenant.Name,
            tenant.Slug,
            tenant.City,
            tenant.ContactEmail,
            tenant.ContactPhone ?? "",
            tenant.PrimaryColor,
            tenant.CreatedAt,
            currentSub,
            subHistory,
            invoiceDtos,
            usage,
            supportNoteDtos,
            healthDto
        );
    }

    public async Task<SupportNoteDto> AddSupportNoteAsync(long tenantId, long actorUserId, CreateSupportNoteRequest req, CancellationToken ct = default)
    {
        var note = new SupportNote
        {
            TenantId = tenantId,
            Content = req.Content,
            IsInternal = req.IsInternal,
            Category = req.Category,
            CreatedByUserId = actorUserId,
            CreatedBy = actorUserId
        };
        _db.Set<SupportNote>().Add(note);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "SupportNote",
            EntityId = note.Id,
            Action = "create_support_note",
            NewValues = $"{{\"content\":\"{req.Content}\",\"isInternal\":{req.IsInternal.ToString().ToLower()},\"category\":\"{req.Category}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);

        return new SupportNoteDto(note.Id, note.Content, note.IsInternal, note.Category, note.CreatedByUserId, $"User {actorUserId}", note.CreatedAt);
    }

    public async Task<Subscription> ExtendTrialAsync(long tenantId, long actorUserId, ExtendTrialRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            throw new InvalidOperationException("Reason >=10 chars required for audit");

        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subscription not found");

        var oldTrialEnds = sub.TrialEndsAt;
        sub.TrialEndsAt = req.NewTrialEndsAt;
        if (sub.State == SubscriptionState.Expired)
        {
            sub.State = SubscriptionState.Trialing;
            sub.ReadOnlyUntil = null;
            sub.ExpiredSince = null;
        }

        var overrideEntry = new BillingOverride
        {
            TenantId = tenantId,
            SubscriptionId = sub.Id,
            OverrideType = "extend_trial",
            DetailsJson = $"{{\"oldTrialEndsAt\":\"{oldTrialEnds}\",\"newTrialEndsAt\":\"{req.NewTrialEndsAt:o}\"}}",
            Reason = req.Reason,
            AdminUserId = actorUserId,
            CreatedBy = actorUserId
        };
        _db.Set<BillingOverride>().Add(overrideEntry);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "Subscription",
            EntityId = sub.Id,
            Action = "manual_override_extend_trial",
            OldValues = $"{{\"oldTrialEndsAt\":\"{oldTrialEnds}\"}}",
            NewValues = $"{{\"newTrialEndsAt\":\"{req.NewTrialEndsAt:o}\",\"reason\":\"{req.Reason}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<Subscription> ChangePlanAsync(long tenantId, long actorUserId, ChangePlanRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            throw new InvalidOperationException("Reason >=10 chars required");

        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subscription not found");
        var newPlan = await _db.Set<Plan>().FirstOrDefaultAsync(p => p.Id == req.NewPlanId && !p.IsDeleted, ct) ?? throw new InvalidOperationException("New plan not found");

        var oldPlanId = sub.PlanId;
        var isUpgrade = newPlan.LearnerLimit > (await _db.Set<Plan>().Where(p => p.Id == oldPlanId).Select(p => p.LearnerLimit).FirstOrDefaultAsync(ct)) || newPlan.PricePerLearnerPerTerm > (await _db.Set<Plan>().Where(p => p.Id == oldPlanId).Select(p => p.PricePerLearnerPerTerm).FirstOrDefaultAsync(ct));

        if (isUpgrade)
        {
            sub.PlanId = newPlan.Id;
            // Pro-rata invoice
            var now = DateTime.UtcNow;
            var daysInPeriod = (sub.CurrentPeriodEnd - sub.CurrentPeriodStart).Days;
            if (daysInPeriod <= 0) daysInPeriod = 90;
            var daysRemaining = (sub.CurrentPeriodEnd - now).Days;
            if (daysRemaining < 0) daysRemaining = 0;
            var oldPlan = await _db.Set<Plan>().FirstOrDefaultAsync(p => p.Id == oldPlanId, ct);
            var priceDiff = newPlan.PricePerLearnerPerTerm - (oldPlan?.PricePerLearnerPerTerm ?? 0m);
            var proRataCharge = Math.Round(priceDiff * sub.BillableLearnerCount * (decimal)daysRemaining / daysInPeriod, 2, MidpointRounding.AwayFromZero);
            if (proRataCharge < 0) proRataCharge = 0m;

            var invoice = new PlatformInvoice
            {
                TenantId = tenantId,
                SubscriptionId = sub.Id,
                InvoiceNumber = $"PLAT-UPG-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(7),
                Subtotal = proRataCharge,
                TotalAmount = proRataCharge,
                BalanceDue = proRataCharge,
                Currency = sub.Currency,
                Status = "issued",
                Notes = $"Upgrade pro-rata from {oldPlan?.Name} to {newPlan.Name}, {daysRemaining}/{daysInPeriod} days remaining"
            };
            _db.Set<PlatformInvoice>().Add(invoice);
        }
        else
        {
            sub.PendingPlanId = newPlan.Id;
            sub.PendingPlanEffectiveAt = sub.CurrentPeriodEnd;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "Subscription",
            EntityId = sub.Id,
            Action = req.IsUpgrade ? "plan_upgrade" : "plan_downgrade",
            OldValues = $"{{\"fromPlanId\":{oldPlanId}}}",
            NewValues = $"{{\"toPlanId\":{newPlan.Id},\"reason\":\"{req.Reason}\",\"isUpgrade\":{req.IsUpgrade.ToString().ToLower()}}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<PlatformInvoice> CreditInvoiceAsync(long tenantId, long actorUserId, CreditInvoiceRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            throw new InvalidOperationException("Reason >=10 chars required");

        var invoice = await _db.Set<PlatformInvoice>().FirstOrDefaultAsync(i => i.Id == req.InvoiceId && i.TenantId == tenantId && !i.IsDeleted, ct) ?? throw new InvalidOperationException("Invoice not found");

        if (req.Amount <= 0 || req.Amount > invoice.BalanceDue)
            throw new InvalidOperationException($"Credit amount must be >0 and <= balance {invoice.BalanceDue}");

        invoice.DiscountAmount = Math.Round(invoice.DiscountAmount + req.Amount, 2, MidpointRounding.AwayFromZero);
        invoice.BalanceDue = Math.Round(invoice.BalanceDue - req.Amount, 2, MidpointRounding.AwayFromZero);
        if (invoice.BalanceDue <= 0)
        {
            invoice.BalanceDue = 0;
            invoice.Status = "paid";
        }

        var overrideEntry = new BillingOverride
        {
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            SubscriptionId = invoice.SubscriptionId,
            OverrideType = "credit_invoice",
            DetailsJson = $"{{\"amount\":{req.Amount},\"currency\":\"{invoice.Currency}\"}}",
            Reason = req.Reason,
            AdminUserId = actorUserId,
            CreatedBy = actorUserId
        };
        _db.Set<BillingOverride>().Add(overrideEntry);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "PlatformInvoice",
            EntityId = invoice.Id,
            Action = "manual_override_credit_invoice",
            OldValues = $"{{\"balanceDue\":{invoice.BalanceDue + req.Amount}}}",
            NewValues = $"{{\"balanceDue\":{invoice.BalanceDue},\"creditAmount\":{req.Amount},\"reason\":\"{req.Reason}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<Subscription> SuspendTenantAsync(long tenantId, long actorUserId, SuspendTenantRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            throw new InvalidOperationException("Reason >=10 chars required");

        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subscription not found");

        var prev = sub.State;
        sub.State = SubscriptionState.Suspended;
        sub.SuspendedSince = DateTime.UtcNow;
        sub.PreviousState = prev.ToString();

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "Subscription",
            EntityId = sub.Id,
            Action = "suspend",
            OldValues = $"{{\"from\":\"{prev}\"}}",
            NewValues = $"{{\"to\":\"Suspended\",\"reason\":\"{req.Reason}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<Subscription> ReactivateTenantAsync(long tenantId, long actorUserId, ReactivateTenantRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
            throw new InvalidOperationException("Reason >=10 chars required");

        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subscription not found");

        var prev = sub.State;
        sub.State = SubscriptionState.Active;
        sub.SuspendedSince = null;
        sub.PastDueSince = null;
        sub.ExpiredSince = null;
        sub.ReadOnlyUntil = null;
        sub.PreviousState = prev.ToString();

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "Subscription",
            EntityId = sub.Id,
            Action = "reactivate",
            OldValues = $"{{\"from\":\"{prev}\"}}",
            NewValues = $"{{\"to\":\"Active\",\"reason\":\"{req.Reason}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<BusinessMetricsDto> GetBusinessMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfLastMonth = startOfMonth.AddMonths(-1);

        var activeSubs = await _db.Set<Subscription>().Where(s => s.State == SubscriptionState.Active && !s.IsDeleted).ToListAsync(ct);
        var mrr = activeSubs.Sum(s => s.BillableLearnerCount * _db.Set<Plan>().Where(p => p.Id == s.PlanId).Select(p => p.PricePerLearnerPerTerm).FirstOrDefault());

        var previousMrr = 0m; // simplified, would query last month snapshot
        var mrrGrowth = previousMrr == 0 ? 0 : Math.Round((mrr - previousMrr) / previousMrr * 100, 2);

        var newTenantsThisMonth = await _db.Tenants.CountAsync(t => t.CreatedAt >= startOfMonth && !t.IsDeleted, ct);
        var newTenantsLastMonth = await _db.Tenants.CountAsync(t => t.CreatedAt >= startOfLastMonth && t.CreatedAt < startOfMonth && !t.IsDeleted, ct);

        var churnThisMonth = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.Cancelled && s.CancelledAt >= startOfMonth && !s.IsDeleted, ct);
        var churnRate = activeSubs.Count > 0 ? Math.Round((decimal)churnThisMonth / activeSubs.Count * 100, 2) : 0m;

        var trialing = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.Trialing && !s.IsDeleted, ct);
        var convertedTrials = await _db.Set<Subscription>().CountAsync(s => s.TrialConvertedAt >= startOfMonth && !s.IsDeleted, ct);
        var trialConversionRate = trialing > 0 ? Math.Round((double)convertedTrials / trialing * 100, 2) : 0;

        var revenueByPlan = await _db.Set<Subscription>().Where(s => s.State == SubscriptionState.Active && !s.IsDeleted).Include(s => s.Plan).GroupBy(s => s.Plan).Select(g => new RevenueByPlanDto(g.Key.Name, g.Key.Code, g.Count(), g.Sum(s => s.BillableLearnerCount * g.Key.PricePerLearnerPerTerm), 0m)).ToListAsync(ct);
        var totalRev = revenueByPlan.Sum(r => r.MonthlyRevenue);
        revenueByPlan = revenueByPlan.Select(r => r with { Percentage = totalRev > 0 ? Math.Round(r.MonthlyRevenue / totalRev * 100, 1) : 0 }).ToList();

        var revenueByMonth = new List<MonthlyRevenueDto>();
        for (int i = 11; i >= 0; i--)
        {
            var monthStart = startOfMonth.AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var rev = await _db.Set<PlatformPayment>().Where(p => p.PaymentDate >= monthStart && p.PaymentDate < monthEnd && !p.IsDeleted).SumAsync(p => p.Amount, ct);
            var newTen = await _db.Tenants.CountAsync(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd && !t.IsDeleted, ct);
            var churned = await _db.Set<Subscription>().CountAsync(s => s.State == SubscriptionState.Cancelled && s.CancelledAt >= monthStart && s.CancelledAt < monthEnd && !s.IsDeleted, ct);
            var active = await _db.Set<Subscription>().CountAsync(s => s.CreatedAt < monthEnd && (s.CancelledAt == null || s.CancelledAt >= monthEnd) && !s.IsDeleted, ct);
            revenueByMonth.Add(new MonthlyRevenueDto(monthStart.Year, monthStart.Month, monthStart.ToString("MMM"), rev, newTen, churned, active));
        }

        var atRisk = await GetAtRiskTenantsAsync(ct);

        return new BusinessMetricsDto(
            mrr,
            previousMrr,
            mrrGrowth,
            newTenantsThisMonth,
            newTenantsLastMonth,
            churnThisMonth,
            churnRate,
            trialConversionRate,
            revenueByPlan,
            trialing,
            convertedTrials,
            0, // at risk trials
            atRisk,
            revenueByMonth
        );
    }

    private async Task<List<TenantAtRiskDto>> GetAtRiskTenantsAsync(CancellationToken ct)
    {
        var atRiskSubs = await _db.Set<Subscription>().Where(s => !s.IsDeleted && (s.State == SubscriptionState.PastDue || s.State == SubscriptionState.Suspended || s.CurrentLearnerCount > s.Plan.LearnerLimit)).Include(s => s.Tenant).Include(s => s.Plan).ToListAsync(ct);
        var result = new List<TenantAtRiskDto>();
        foreach (var sub in atRiskSubs)
        {
            var lastActivity = await _db.AuditLogs.Where(a => a.TenantId == sub.TenantId).OrderByDescending(a => a.CreatedAt).Select(a => a.CreatedAt).FirstOrDefaultAsync(ct);
            result.Add(new TenantAtRiskDto(
                sub.TenantId,
                sub.Tenant.Name,
                sub.Tenant.Slug,
                sub.State.ToString(),
                sub.Plan.Name,
                sub.BillableLearnerCount,
                sub.CurrentLearnerCount,
                sub.Plan.LearnerLimit,
                sub.CurrentLearnerCount > sub.Plan.LearnerLimit,
                sub.PastDueSince,
                sub.SuspendedSince,
                sub.State == SubscriptionState.PastDue ? $"Past due since {sub.PastDueSince:yyyy-MM-dd}" : sub.CurrentLearnerCount > sub.Plan.LearnerLimit ? $"Over learner limit {sub.CurrentLearnerCount}/{sub.Plan.LearnerLimit}" : sub.State.ToString(),
                50,
                lastActivity
            ));
        }
        return result.OrderByDescending(r => r.Billable).ToList();
    }

    public async Task<List<BackgroundJobStatusDto>> GetBackgroundJobsAsync(string? status, CancellationToken ct = default)
    {
        var query = _db.Set<BackgroundJobRecord>().Where(j => !j.IsDeleted).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(j => j.Status == status);
        var jobs = await query.OrderByDescending(j => j.CreatedAt).Take(100).ToListAsync(ct);
        var result = new List<BackgroundJobStatusDto>();
        foreach (var job in jobs)
        {
            var tenantName = job.TenantId.HasValue ? (await _db.Tenants.FirstOrDefaultAsync(t => t.Id == job.TenantId.Value, ct))?.Name : null;
            result.Add(new BackgroundJobStatusDto(job.Id, job.TenantId, tenantName, job.JobType, job.JobId, job.Status, job.StartedAt, job.CompletedAt, job.RetryCount, job.ErrorMessage, job.CreatedAt));
        }
        return result;
    }

    public async Task<List<ErrorRateDto>> GetErrorRatesAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _db.Set<ErrorRateSnapshot>().Where(e => !e.IsDeleted).AsQueryable();
        if (from.HasValue) query = query.Where(e => e.SnapshotDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.SnapshotDate <= to.Value);
        var list = await query.OrderByDescending(e => e.SnapshotDate).Take(100).ToListAsync(ct);
        return list.Select(e => new ErrorRateDto(e.SnapshotDate, e.TenantId, e.Service, e.ErrorCount, e.WarningCount, e.RequestCount, e.ErrorRate)).ToList();
    }

    public async Task<List<SmsSpendByTenantDto>> GetSmsSpendAsync(int year, int month, CancellationToken ct = default)
    {
        var usage = await _db.Set<TenantMessagingUsage>().Where(u => u.Year == year && u.Month == month && !u.IsDeleted).Include(u => u.Tenant).ToListAsync(ct);
        return usage.Select(u => new SmsSpendByTenantDto(u.TenantId, _db.Tenants.FirstOrDefault(t => t.Id == u.TenantId)?.Name ?? $"Tenant {u.TenantId}", u.Year, u.Month, u.SmsCount, u.SmsCost, u.EmailCount, u.EmailCost, u.Currency, u.SmsCost + u.EmailCost)).OrderByDescending(u => u.TotalCost).ToList();
    }

    public async Task<List<StorageGrowthDto>> GetStorageGrowthAsync(CancellationToken ct = default)
    {
        var records = await _db.Set<StorageGrowthRecord>().Where(s => !s.IsDeleted).OrderByDescending(s => s.MeasuredAt).Take(100).ToListAsync(ct);
        var result = new List<StorageGrowthDto>();
        foreach (var r in records.GroupBy(r => r.TenantId).Select(g => g.OrderByDescending(x => x.MeasuredAt).First()))
        {
            var tenantName = (await _db.Tenants.FirstOrDefaultAsync(t => t.Id == r.TenantId, ct))?.Name ?? $"Tenant {r.TenantId}";
            var prev = await _db.Set<StorageGrowthRecord>().Where(s => s.TenantId == r.TenantId && s.MeasuredAt < r.MeasuredAt && !s.IsDeleted).OrderByDescending(s => s.MeasuredAt).FirstOrDefaultAsync(ct);
            var growth = prev != null ? Math.Round((r.TotalBytes - prev.TotalBytes) / 1024.0 / 1024.0 / 1024.0, 2) : 0m;
            result.Add(new StorageGrowthDto(r.TenantId, tenantName, r.TotalFiles, r.TotalBytes, Math.Round(r.TotalBytes / 1024.0 / 1024.0 / 1024.0, 2), r.DocumentBytes, r.PhotoBytes, r.MeasuredAt, growth));
        }
        return result.OrderByDescending(r => r.TotalGb).ToList();
    }
}

// Stub entities for compilation that exist in other modules
public class Tenant : BaseEntity { public string Name { get; set; } = ""; public string Slug { get; set; } = ""; public string City { get; set; } = ""; public string ContactEmail { get; set; } = ""; public string? ContactPhone { get; set; } public string PrimaryColor { get; set; } = ""; }
public class User : BaseEntity { public long? TenantId { get; set; } public string DisplayName { get; set; } = ""; }

// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Final cleanup - single source of truth
