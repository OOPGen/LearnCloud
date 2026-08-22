using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Services;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.PlatformBilling.Jobs;

// Dunning: scheduled job that moves overdue subscriptions to past_due, sends escalating reminders, then suspends after configurable grace period
// Trial: 14 days no card with reminders at day7,12,expiry and 30-day read-only window after expiry before archival

public class DunningJob
{
    private readonly LearnCloudDbContext _db;
    private readonly ILogger<DunningJob> _logger;

    public DunningJob(LearnCloudDbContext db, ILogger<DunningJob> logger) { _db = db; _logger = logger; }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await ProcessTrialsAsync(ct);
        await ProcessOverdueInvoicesAsync(ct);
        await ProcessPastDueGraceAsync(ct);
        await ProcessExpiredReadOnlyWindowAsync(ct);
    }

    private async Task ProcessTrialsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var trialingSubs = await _db.Set<Subscription>().Where(s=>s.State==SubscriptionState.Trialing && !s.IsDeleted).ToListAsync(ct);

        foreach (var sub in trialingSubs)
        {
            if (!sub.TrialEndsAt.HasValue) continue;

            var daysLeft = (sub.TrialEndsAt.Value - now).Days;
            var daysSinceStart = (now - (sub.TrialStartedAt ?? now)).Days;

            // Reminders at day 7, 12, expiry
            if (daysSinceStart == 7)
            {
                await SendDunningEvent(sub, "trial_reminder_day_7", $"Trial reminder day 7: {daysLeft} days left", ct);
            }
            else if (daysSinceStart == 12)
            {
                await SendDunningEvent(sub, "trial_reminder_day_12", $"Trial reminder day 12: {daysLeft} days left, expires soon", ct);
            }

            if (now >= sub.TrialEndsAt.Value)
            {
                // Trial expired -> expired with 30-day read-only window
                try
                {
                    var prev = sub.State;
                    SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Expired, BillingTrigger.TrialExpired, "Trial 14 days ended");
                    sub.ExpiredSince = now;
                    sub.ReadOnlyUntil = now.AddDays(30);

                    _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
                    {
                        TenantId = sub.TenantId,
                        EntityType = "Subscription",
                        EntityId = sub.Id,
                        Action = "trial_expired",
                        OldValues = $"{{\"from\":\"{prev}\"}}",
                        NewValues = $"{{\"to\":\"Expired\",\"readOnlyUntil\":\"{sub.ReadOnlyUntil:o}\"}}"
                    });

                    await SendDunningEvent(sub, "trial_expired", $"Trial expired. Your school is now read-only for 30 days. Pay to reactivate and keep data. Payment link: https://{sub.Tenant.Slug}.learncloud.co.zw/billing", ct);

                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Trial expired for tenant {TenantId}, moved to Expired with read-only until {ReadOnlyUntil}", sub.TenantId, sub.ReadOnlyUntil);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to expire trial for tenant {TenantId}", sub.TenantId);
                }
            }
        }
    }

    private async Task ProcessOverdueInvoicesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow.Date;
        var overdueInvoices = await _db.Set<PlatformInvoice>()
            .Where(i=>!i.IsDeleted && i.Status!="paid" && i.Status!="void" && i.DueDate < now && i.BalanceDue>0)
            .Include(i=>i.Subscription)
            .ToListAsync(ct);

        foreach (var invoice in overdueInvoices)
        {
            var sub = invoice.Subscription;
            if (sub == null) continue;
            if (sub.State == SubscriptionState.Active)
            {
                try
                {
                    SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.PastDue, BillingTrigger.InvoiceOverdue, $"Invoice {invoice.InvoiceNumber} overdue due {invoice.DueDate:yyyy-MM-dd}");
                    sub.PastDueSince = now;

                    await SendDunningEvent(sub, "invoice_overdue", $"Invoice {invoice.InvoiceNumber} overdue since {invoice.DueDate:yyyy-MM-dd}, amount {invoice.BalanceDue} {invoice.Currency}. Please pay. Link: https://{sub.Tenant.Slug}.learncloud.co.zw/billing", ct, invoice.Id);

                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move to PastDue for tenant {TenantId}", sub.TenantId);
                }
            }
            else if (sub.State == SubscriptionState.PastDue)
            {
                // Escalating reminders while past due
                var daysPastDue = (now - (sub.PastDueSince?.Date ?? invoice.DueDate)).Days;
                if (daysPastDue % 3 == 0) // every 3 days
                {
                    await SendDunningEvent(sub, $"past_due_day_{daysPastDue}", $"Reminder: Invoice {invoice.InvoiceNumber} past due {daysPastDue} days, balance {invoice.BalanceDue}. Pay to avoid suspension.", ct, invoice.Id);
                }
            }
        }
    }

    private async Task ProcessPastDueGraceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow.Date;
        var pastDueSubs = await _db.Set<Subscription>().Where(s=>s.State==SubscriptionState.PastDue && !s.IsDeleted && s.PastDueSince!=null).ToListAsync(ct);

        foreach (var sub in pastDueSubs)
        {
            var graceEnd = sub.PastDueSince!.Value.AddDays(sub.PastDueGraceDays);
            if (now >= graceEnd)
            {
                try
                {
                    SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Suspended, BillingTrigger.GracePeriodEnded, $"Past due grace {sub.PastDueGraceDays} days ended");
                    sub.SuspendedSince = now;

                    await SendDunningEvent(sub, "suspended", $"Your school account has been suspended due to non-payment. Tenant is now read-only with banner and payment link. Data never deleted. Pay to reactivate. Link: https://{sub.Tenant.Slug}.learncloud.co.zw/billing/payment-link", ct);

                    await _db.SaveChangesAsync(ct);
                    _logger.LogWarning("Subscription {SubId} tenant {TenantId} suspended after grace", sub.Id, sub.TenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to suspend tenant {TenantId}", sub.TenantId);
                }
            }
            else
            {
                // Suspension warning before hit
                var daysLeft = (graceEnd - now).Days;
                if (daysLeft == 2 || daysLeft == 1)
                {
                    await SendDunningEvent(sub, $"suspension_warning_{daysLeft}d", $"Warning: Your account will be suspended in {daysLeft} days if payment not received. Read-only mode after suspension, data never deleted, payment link will remain.", ct);
                }
            }
        }

        // Suspended -> Cancelled after suspension grace
        var suspendedSubs = await _db.Set<Subscription>().Where(s=>s.State==SubscriptionState.Suspended && s.SuspendedSince!=null && !s.IsDeleted).ToListAsync(ct);
        foreach (var sub in suspendedSubs)
        {
            var cancelAt = sub.SuspendedSince!.Value.AddDays(sub.SuspensionGraceDays);
            if (now >= cancelAt)
            {
                try
                {
                    SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Cancelled, BillingTrigger.SuspensionGraceEnded, $"Suspension grace {sub.SuspensionGraceDays} days ended, no payment");
                    await _db.SaveChangesAsync(ct);
                    await SendDunningEvent(sub, "cancelled_after_suspension", $"Account cancelled after suspension grace. Data retained, read-only for 30 days before archival. Contact support to win-back.", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cancel suspended tenant {TenantId}", sub.TenantId);
                }
            }
        }
    }

    private async Task ProcessExpiredReadOnlyWindowAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow.Date;
        var expiredSubs = await _db.Set<Subscription>().Where(s=>s.State==SubscriptionState.Expired && s.ReadOnlyUntil!=null && !s.IsDeleted).ToListAsync(ct);

        foreach (var sub in expiredSubs)
        {
            if (now >= sub.ReadOnlyUntil!.Value.Date)
            {
                try
                {
                    SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Archived, BillingTrigger.ReadOnlyWindowEnded, "30-day read-only window after expiry ended, archival");
                    await _db.SaveChangesAsync(ct);
                    await SendDunningEvent(sub, "archived", $"Account archived after 30-day read-only window. Data retained for legal 7 years but not accessible. Contact support to reactivate.", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to archive expired tenant {TenantId}", sub.TenantId);
                }
            }
        }
    }

    private async Task SendDunningEvent(Subscription sub, string eventType, string body, CancellationToken ct, long? invoiceId = null)
    {
        var tenant = await _db.Set<Tenant>().FirstOrDefaultAsync(t=>t.Id==sub.TenantId, ct);
        var recipient = tenant?.ContactEmail ?? "admin@school.co.zw";

        var dunning = new DunningEvent
        {
            TenantId = sub.TenantId,
            SubscriptionId = sub.Id,
            InvoiceId = invoiceId,
            EventType = eventType,
            Channel = "email",
            Recipient = recipient,
            Subject = $"LearnCloud Billing - {eventType}",
            Body = body,
            SentAt = DateTime.UtcNow,
            IsSuccess = true
        };
        _db.Set<DunningEvent>().Add(dunning);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Dunning event {EventType} for tenant {TenantId}: {Body}", eventType, sub.TenantId, body);
    }
}
