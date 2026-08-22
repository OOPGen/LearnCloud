using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformBilling.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformBilling.Services;

public interface IBillingService
{
    Task<PlatformInvoice> CreateInvoiceAsync(long tenantId, long subscriptionId, int billableCount, long? academicYearId, long? termId, CancellationToken ct = default);
    Task<PlatformInvoice> RecordPaymentAsync(long tenantId, long invoiceId, decimal amount, string method, string? reference, long actorUserId, CancellationToken ct = default);
    Task<List<PlatformInvoice>> GetInvoicesAsync(long tenantId, CancellationToken ct = default);
}

public class BillingService : IBillingService
{
    private readonly LearnCloudDbContext _db;

    public BillingService(LearnCloudDbContext db) => _db = db;

    public async Task<PlatformInvoice> CreateInvoiceAsync(long tenantId, long subscriptionId, int billableCount, long? academicYearId, long? termId, CancellationToken ct = default)
    {
        var sub = await _db.Set<Subscription>().Include(s=>s.Plan).FirstOrDefaultAsync(s=>s.Id==subscriptionId && s.TenantId==tenantId, ct) ?? throw new InvalidOperationException("Subscription not found");
        var plan = sub.Plan;

        // Billing period aligned to school term, enrolment snapshot at period start used as billable count
        // Price per learner per term * billable, but minimum charge
        var subtotal = Math.Round(billableCount * plan.PricePerLearnerPerTerm, 2, MidpointRounding.AwayFromZero);
        var total = subtotal < plan.MinimumCharge ? plan.MinimumCharge : subtotal;
        var minimumApplied = total == plan.MinimumCharge && subtotal < plan.MinimumCharge ? plan.MinimumCharge - subtotal : 0m;

        var invoiceNumber = await GetNextInvoiceNumberAsync(tenantId, ct);

        var invoice = new PlatformInvoice
        {
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            InvoiceNumber = invoiceNumber,
            AcademicYearId = academicYearId,
            TermId = termId,
            IssueDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(14), // due 14 days
            Subtotal = subtotal,
            MinimumChargeApplied = minimumApplied,
            TotalAmount = total,
            BalanceDue = total,
            Currency = plan.Currency,
            Status = "issued",
            Notes = $"Billable learners snapshot {billableCount} at period start {sub.CurrentPeriodStart:yyyy-MM-dd}, plan {plan.Name} @ {plan.PricePerLearnerPerTerm}/learner, min {plan.MinimumCharge}"
        };
        _db.Set<PlatformInvoice>().Add(invoice);
        await _db.SaveChangesAsync(ct);

        // Line item
        var line = new PlatformInvoiceLine
        {
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            Description = $"{plan.Name} - {billableCount} learners @ {plan.PricePerLearnerPerTerm:F2}/learner - Term {(termId.HasValue? termId.ToString():"N/A")} (billable snapshot {billableCount})",
            Quantity = billableCount,
            UnitPrice = plan.PricePerLearnerPerTerm,
            LineTotal = subtotal,
            Currency = plan.Currency,
            MetadataJson = $"{{\"billable\":{billableCount},\"plan\":\"{plan.Code}\",\"minCharge\":{plan.MinimumCharge}}}"
        };
        _db.Set<PlatformInvoiceLine>().Add(line);

        if (minimumApplied > 0)
        {
            var minLine = new PlatformInvoiceLine
            {
                TenantId = tenantId,
                InvoiceId = invoice.Id,
                Description = $"Minimum charge adjustment - plan minimum {plan.MinimumCharge} vs subtotal {subtotal}",
                Quantity = 1,
                UnitPrice = minimumApplied,
                LineTotal = minimumApplied,
                Currency = plan.Currency
            };
            _db.Set<PlatformInvoiceLine>().Add(minLine);
        }

        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<PlatformInvoice> RecordPaymentAsync(long tenantId, long invoiceId, decimal amount, string method, string? reference, long actorUserId, CancellationToken ct = default)
    {
        var invoice = await _db.Set<PlatformInvoice>().FirstOrDefaultAsync(i=>i.Id==invoiceId && i.TenantId==tenantId, ct) ?? throw new InvalidOperationException("Invoice not found");

        var payment = new PlatformPayment
        {
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = Math.Round(amount,2,MidpointRounding.AwayFromZero),
            Currency = invoice.Currency,
            Method = method,
            Reference = reference,
            PaymentDate = DateTime.UtcNow.Date,
            Status = "confirmed"
        };
        _db.Set<PlatformPayment>().Add(payment);

        invoice.AmountPaid = Math.Round(invoice.AmountPaid + amount,2,MidpointRounding.AwayFromZero);
        invoice.BalanceDue = Math.Round(invoice.TotalAmount - invoice.AmountPaid,2,MidpointRounding.AwayFromZero);
        if (invoice.BalanceDue <= 0)
        {
            invoice.BalanceDue = 0;
            invoice.Status = "paid";
        }
        else if (invoice.AmountPaid >0)
        {
            invoice.Status = "partial";
        }

        // Audit
        _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "PlatformInvoice",
            EntityId = invoice.Id,
            Action = "payment_recorded",
            NewValues = $"{{\"amount\":{amount},\"method\":\"{method}\",\"reference\":\"{reference}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);

        // If invoice paid and subscription past_due/suspended, transition to active
        if (invoice.Status == "paid")
        {
            var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s=>s.Id==invoice.SubscriptionId, ct);
            if (sub != null && (sub.State == SubscriptionState.PastDue || sub.State == SubscriptionState.Suspended || sub.State == SubscriptionState.Expired))
            {
                try
                {
                    SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Active, BillingTrigger.PaymentReceived, $"Invoice {invoice.InvoiceNumber} paid");
                    await _db.SaveChangesAsync(ct);
                }
                catch { /* ignore if transition not allowed */ }
            }
        }

        return invoice;
    }

    public async Task<List<PlatformInvoice>> GetInvoicesAsync(long tenantId, CancellationToken ct = default)
    {
        return await _db.Set<PlatformInvoice>().Where(i=>i.TenantId==tenantId && !i.IsDeleted).OrderByDescending(i=>i.IssueDate).ToListAsync(ct);
    }

    private async Task<string> GetNextInvoiceNumberAsync(long tenantId, CancellationToken ct)
    {
        // Simple sequence per tenant per year
        var year = DateTime.UtcNow.Year;
        var count = await _db.Set<PlatformInvoice>().CountAsync(i=>i.TenantId==tenantId && i.IssueDate.Year==year, ct);
        return $"PLAT-{year}-{(count+1):D5}";
    }
}
