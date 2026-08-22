using LearnCloud.PlatformBilling.Entities;

namespace LearnCloud.PlatformBilling.Services;

// Define every legal transition and what triggers it
public class SubscriptionTransition
{
    public SubscriptionState From { get; set; }
    public SubscriptionState To { get; set; }
    public BillingTrigger Trigger { get; set; }
    public string Description { get; set; } = "";
    public Func<Subscription, bool>? Guard { get; set; } // additional guard
}

public static class SubscriptionStateMachine
{
    // All legal transitions - exhaustive
    public static readonly List<SubscriptionTransition> Transitions = new()
    {
        // Trialing
        new SubscriptionTransition { From = SubscriptionState.Trialing, To = SubscriptionState.Active, Trigger = BillingTrigger.TrialConverted, Description = "Trial converted: school pays first invoice, payment received during trial, 14-day trial no card but converts when invoice paid" },
        new SubscriptionTransition { From = SubscriptionState.Trialing, To = SubscriptionState.Active, Trigger = BillingTrigger.PaymentReceived, Description = "Payment received during trial converts to active immediately" },
        new SubscriptionTransition { From = SubscriptionState.Trialing, To = SubscriptionState.Expired, Trigger = BillingTrigger.TrialExpired, Description = "Trial 14 days ended without payment, moves to expired with 30-day read-only window" },
        new SubscriptionTransition { From = SubscriptionState.Trialing, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledBySchool, Description = "School cancels during trial" },
        new SubscriptionTransition { From = SubscriptionState.Trialing, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledByAdmin, Description = "Admin cancels trial, e.g. fraud" },

        // Active
        new SubscriptionTransition { From = SubscriptionState.Active, To = SubscriptionState.PastDue, Trigger = BillingTrigger.InvoiceOverdue, Description = "Invoice past due_date, dunning job moves overdue to past_due" },
        new SubscriptionTransition { From = SubscriptionState.Active, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledBySchool, Description = "School cancels active subscription, effective at next period or immediate per policy" },
        new SubscriptionTransition { From = SubscriptionState.Active, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledByAdmin, Description = "Admin cancels active, e.g. ToS violation" },
        new SubscriptionTransition { From = SubscriptionState.Active, To = SubscriptionState.Active, Trigger = BillingTrigger.PlanUpgraded, Description = "Upgrade takes effect immediately with pro-rata charge, stays active" },
        new SubscriptionTransition { From = SubscriptionState.Active, To = SubscriptionState.Active, Trigger = BillingTrigger.PlanDowngraded, Description = "Downgrade takes effect at next period, stays active now, pending plan set" },

        // PastDue
        new SubscriptionTransition { From = SubscriptionState.PastDue, To = SubscriptionState.Active, Trigger = BillingTrigger.PaymentReceived, Description = "Payment received, invoice paid, back to active" },
        new SubscriptionTransition { From = SubscriptionState.PastDue, To = SubscriptionState.Suspended, Trigger = BillingTrigger.GracePeriodEnded, Description = "Past due grace period (7 days configurable) ended, no payment, suspends tenant to read-only with banner and payment link" },
        new SubscriptionTransition { From = SubscriptionState.PastDue, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledBySchool, Description = "School cancels while past due" },
        new SubscriptionTransition { From = SubscriptionState.PastDue, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledByAdmin, Description = "Admin cancels past due" },

        // Suspended - read-only with clear banner and payment link. Never delete data, never lock out entirely
        new SubscriptionTransition { From = SubscriptionState.Suspended, To = SubscriptionState.Active, Trigger = BillingTrigger.PaymentReceived, Description = "Payment received while suspended, back to active, read-only removed" },
        new SubscriptionTransition { From = SubscriptionState.Suspended, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.SuspensionGraceEnded, Description = "Suspension grace (30 days) ended, no payment, moves to cancelled but data retained" },
        new SubscriptionTransition { From = SubscriptionState.Suspended, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledByAdmin, Description = "Admin cancels suspended" },
        new SubscriptionTransition { From = SubscriptionState.Suspended, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledBySchool, Description = "School cancels while suspended" },

        // Expired - 14-day trial ended, 30-day read-only window before archival
        new SubscriptionTransition { From = SubscriptionState.Expired, To = SubscriptionState.Active, Trigger = BillingTrigger.PaymentReceived, Description = "School pays during 30-day read-only window after expiry, win-back, converts expired to active" },
        new SubscriptionTransition { From = SubscriptionState.Expired, To = SubscriptionState.Active, Trigger = BillingTrigger.Reactivated, Description = "Admin reactivates expired" },
        new SubscriptionTransition { From = SubscriptionState.Expired, To = SubscriptionState.Archived, Trigger = BillingTrigger.ReadOnlyWindowEnded, Description = "30-day read-only window after expiry ended, moves to archived (data retained but not accessible, before final deletion)" },
        new SubscriptionTransition { From = SubscriptionState.Expired, To = SubscriptionState.Cancelled, Trigger = BillingTrigger.CancelledByAdmin, Description = "Admin cancels expired" },

        // Cancelled terminal but can be reactivated by admin for win-back
        new SubscriptionTransition { From = SubscriptionState.Cancelled, To = SubscriptionState.Active, Trigger = BillingTrigger.Reactivated, Description = "Admin reactivates cancelled subscription for win-back" },
        new SubscriptionTransition { From = SubscriptionState.Cancelled, To = SubscriptionState.Archived, Trigger = BillingTrigger.ReadOnlyWindowEnded, Description = "Cancelled moves to archived after retention period" },

        // Archived - final state before deletion (data still retained for legal 7 years, but not accessible)
        new SubscriptionTransition { From = SubscriptionState.Archived, To = SubscriptionState.Active, Trigger = BillingTrigger.Reactivated, Description = "Admin reactivates archived (restore)" },

        // Manual override transitions
        new SubscriptionTransition { From = SubscriptionState.Trialing, To = SubscriptionState.Trialing, Trigger = BillingTrigger.ManualOverride, Description = "Extend trial - manual override to extend trial, fully audited, stays trialing" },
        new SubscriptionTransition { From = SubscriptionState.PastDue, To = SubscriptionState.Active, Trigger = BillingTrigger.ManualOverride, Description = "Manual override to extend suspension grace or credit invoice, back to active" },
        new SubscriptionTransition { From = SubscriptionState.Suspended, To = SubscriptionState.Active, Trigger = BillingTrigger.ManualOverride, Description = "Manual override extend suspension grace" },
        new SubscriptionTransition { From = SubscriptionState.Active, To = SubscriptionState.Active, Trigger = BillingTrigger.ManualOverride, Description = "Manual credit invoice" },
    };

    public static bool CanTransition(SubscriptionState from, SubscriptionState to, BillingTrigger trigger)
    {
        return Transitions.Any(t => t.From == from && t.To == to && t.Trigger == trigger);
    }

    public static List<SubscriptionTransition> GetAllowedTransitions(SubscriptionState from)
    {
        return Transitions.Where(t => t.From == from).ToList();
    }

    public static void ValidateTransition(Subscription subscription, SubscriptionState to, BillingTrigger trigger)
    {
        if (!CanTransition(subscription.State, to, trigger))
        {
            throw new InvalidOperationException($"Illegal transition: {subscription.State} -> {to} via {trigger} is not allowed. Allowed from {subscription.State}: {string.Join(", ", GetAllowedTransitions(subscription.State).Select(t => $"{t.To} via {t.Trigger}"))}");
        }

        var transition = Transitions.First(t => t.From == subscription.State && t.To == to && t.Trigger == trigger);
        if (transition.Guard != null && !transition.Guard(subscription))
        {
            throw new InvalidOperationException($"Guard failed for transition {subscription.State} -> {to} via {trigger}: {transition.Description}");
        }
    }

    public static void ApplyTransition(Subscription subscription, SubscriptionState to, BillingTrigger trigger, string? reason = null)
    {
        ValidateTransition(subscription, to, trigger);

        subscription.PreviousState = subscription.State.ToString();
        subscription.State = to;

        switch (to)
        {
            case SubscriptionState.PastDue when trigger == BillingTrigger.InvoiceOverdue:
                subscription.PastDueSince = DateTime.UtcNow;
                break;
            case SubscriptionState.Suspended when trigger == BillingTrigger.GracePeriodEnded:
                subscription.SuspendedSince = DateTime.UtcNow;
                break;
            case SubscriptionState.Expired when trigger == BillingTrigger.TrialExpired:
                subscription.ExpiredSince = DateTime.UtcNow;
                subscription.ReadOnlyUntil = DateTime.UtcNow.AddDays(30); // 30-day read-only window
                break;
            case SubscriptionState.Active when trigger == BillingTrigger.PaymentReceived:
                subscription.PastDueSince = null;
                subscription.SuspendedSince = null;
                subscription.ExpiredSince = null;
                subscription.ReadOnlyUntil = null;
                if (subscription.PreviousState == "Trialing")
                    subscription.TrialConvertedAt = DateTime.UtcNow;
                break;
            case SubscriptionState.Cancelled:
                subscription.CancelledAt = DateTime.UtcNow;
                subscription.CancellationReason = reason;
                break;
        }
    }
}

// Service to handle transitions with auditing
public class SubscriptionService
{
    private readonly LearnCloud.MultiTenancy.Context.LearnCloudDbContext _db;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(LearnCloud.MultiTenancy.Context.LearnCloudDbContext db, ILogger<SubscriptionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Subscription> TransitionAsync(long tenantId, SubscriptionState to, BillingTrigger trigger, long actorUserId, string? reason = null, CancellationToken ct = default)
    {
        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subscription not found");

        var from = sub.State;
        SubscriptionStateMachine.ValidateTransition(sub, to, trigger);
        SubscriptionStateMachine.ApplyTransition(sub, to, trigger, reason);

        // Audit
        _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            EntityType = "Subscription",
            EntityId = sub.Id,
            Action = $"subscription_{trigger.ToString().ToLower()}",
            OldValues = $"{{\"from\":\"{from}\",\"to\":\"{to}\",\"trigger\":\"{trigger}\"}}",
            NewValues = $"{{\"reason\":\"{reason}\",\"readOnlyUntil\":\"{sub.ReadOnlyUntil}\"}}",
            CreatedBy = actorUserId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Subscription {SubId} Tenant {TenantId} transitioned {From}->{To} via {Trigger} by {Actor} reason {Reason}", sub.Id, tenantId, from, to, trigger, actorUserId, reason);

        return sub;
    }

    public async Task<Subscription> ChangePlanAsync(long tenantId, long newPlanId, long actorUserId, bool isUpgrade, string? reason, CancellationToken ct = default)
    {
        var sub = await _db.Set<Subscription>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subscription not found");
        var oldPlanId = sub.PlanId;
        var oldPlan = await _db.Set<Plan>().FirstOrDefaultAsync(p => p.Id == oldPlanId, ct);
        var newPlan = await _db.Set<Plan>().FirstOrDefaultAsync(p => p.Id == newPlanId, ct) ?? throw new InvalidOperationException("New plan not found");

        if (isUpgrade)
        {
            // Upgrade takes effect immediately with pro-rata charge
            var now = DateTime.UtcNow;
            var daysInPeriod = (sub.CurrentPeriodEnd - sub.CurrentPeriodStart).Days;
            var daysRemaining = (sub.CurrentPeriodEnd - now).Days;
            if (daysRemaining < 0) daysRemaining = 0;
            if (daysInPeriod <= 0) daysInPeriod = 90; // fallback term ~90 days

            // Pro-rata: price difference * (daysRemaining / daysInPeriod)
            // Simplified: (newPlan price per learner * billable - oldPlan price * billable) * (daysRemaining/daysInPeriod)
            // All decimal, AwayFromZero
            var oldPrice = oldPlan?.PricePerLearnerPerTerm ?? 0m;
            var newPrice = newPlan.PricePerLearnerPerTerm;
            var learnerCount = sub.BillableLearnerCount;
            var priceDiffPerLearner = newPrice - oldPrice;
            var proRataFactor = (decimal)daysRemaining / daysInPeriod;
            var proRataCharge = Math.Round(priceDiffPerLearner * learnerCount * proRataFactor, 2, MidpointRounding.AwayFromZero);
            if (proRataCharge < 0) proRataCharge = 0m;

            sub.PlanId = newPlanId;
            // Create pro-rata invoice immediately
            var invoice = new PlatformInvoice
            {
                TenantId = tenantId,
                SubscriptionId = sub.Id,
                InvoiceNumber = $"PLAT-UPG-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000,9999)}",
                AcademicYearId = sub.AcademicYearId,
                TermId = sub.TermId,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(7),
                Subtotal = proRataCharge,
                TotalAmount = proRataCharge,
                BalanceDue = proRataCharge,
                Currency = sub.Currency,
                Status = "issued",
                Notes = $"Upgrade pro-rata from {oldPlan?.Name} to {newPlan.Name}, {daysRemaining}/{daysInPeriod} days remaining, {learnerCount} learners"
            };
            _db.Set<PlatformInvoice>().Add(invoice);

            // Log plan change
            _db.Set<PlanChangeLog>().Add(new PlanChangeLog
            {
                TenantId = tenantId,
                SubscriptionId = sub.Id,
                FromPlanId = oldPlanId,
                ToPlanId = newPlanId,
                ChangeType = "upgrade",
                EffectiveType = "immediate",
                EffectiveAt = now,
                ProRataCharge = proRataCharge,
                Reason = reason,
                ChangedByUserId = actorUserId
            });

            SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Active, BillingTrigger.PlanUpgraded, reason);
        }
        else
        {
            // Downgrade takes effect at next period
            sub.PendingPlanId = newPlanId;
            sub.PendingPlanEffectiveAt = sub.CurrentPeriodEnd;

            _db.Set<PlanChangeLog>().Add(new PlanChangeLog
            {
                TenantId = tenantId,
                SubscriptionId = sub.Id,
                FromPlanId = oldPlanId,
                ToPlanId = newPlanId,
                ChangeType = "downgrade",
                EffectiveType = "next_period",
                EffectiveAt = sub.CurrentPeriodEnd,
                Reason = reason,
                ChangedByUserId = actorUserId
            });

            SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.Active, BillingTrigger.PlanDowngraded, reason);
        }

        await _db.SaveChangesAsync(ct);
        return sub;
    }
}
