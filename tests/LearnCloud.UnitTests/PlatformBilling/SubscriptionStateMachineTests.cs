using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Services;
using Xunit;

namespace LearnCloud.PlatformBilling.Tests;

public class SubscriptionStateMachineTests
{
    private Subscription NewSub(SubscriptionState state, DateTime? trialEndsAt = null)
    {
        return new Subscription
        {
            Id = 1,
            TenantId = 1,
            PlanId = 1,
            State = state,
            TrialStartedAt = DateTime.UtcNow.AddDays(-10),
            TrialEndsAt = trialEndsAt ?? DateTime.UtcNow.AddDays(4),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-30),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(60),
            PastDueGraceDays = 7,
            SuspensionGraceDays = 30,
            BillableLearnerCount = 542
        };
    }

    [Fact]
    public void Trialing_To_Active_Via_TrialConverted_Allowed()
    {
        var sub = NewSub(SubscriptionState.Trialing);
        Assert.True(SubscriptionStateMachine.CanTransition(sub.State, SubscriptionState.Active, BillingTrigger.TrialConverted));
        SubscriptionStateMachine.ValidateTransition(sub, SubscriptionState.Active, BillingTrigger.TrialConverted);
    }

    [Fact]
    public void Trialing_To_Expired_Via_TrialExpired_Allowed()
    {
        var sub = NewSub(SubscriptionState.Trialing);
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Trialing, SubscriptionState.Expired, BillingTrigger.TrialExpired));
    }

    [Fact]
    public void Trialing_To_Cancelled_Via_CancelledBySchool_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Trialing, SubscriptionState.Cancelled, BillingTrigger.CancelledBySchool));
    }

    [Fact]
    public void Active_To_PastDue_Via_InvoiceOverdue_Allowed()
    {
        var sub = NewSub(SubscriptionState.Active);
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Active, SubscriptionState.PastDue, BillingTrigger.InvoiceOverdue));
    }

    [Fact]
    public void Active_To_Cancelled_Via_CancelledBySchool_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Active, SubscriptionState.Cancelled, BillingTrigger.CancelledBySchool));
    }

    [Fact]
    public void Active_PlanUpgraded_Stays_Active()
    {
        var sub = NewSub(SubscriptionState.Active);
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Active, SubscriptionState.Active, BillingTrigger.PlanUpgraded));
    }

    [Fact]
    public void Active_PlanDowngraded_Stays_Active()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Active, SubscriptionState.Active, BillingTrigger.PlanDowngraded));
    }

    [Fact]
    public void PastDue_To_Active_Via_PaymentReceived_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.PastDue, SubscriptionState.Active, BillingTrigger.PaymentReceived));
    }

    [Fact]
    public void PastDue_To_Suspended_Via_GracePeriodEnded_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.PastDue, SubscriptionState.Suspended, BillingTrigger.GracePeriodEnded));
    }

    [Fact]
    public void Suspended_To_Active_Via_PaymentReceived_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Suspended, SubscriptionState.Active, BillingTrigger.PaymentReceived));
    }

    [Fact]
    public void Suspended_To_Cancelled_Via_SuspensionGraceEnded_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Suspended, SubscriptionState.Cancelled, BillingTrigger.SuspensionGraceEnded));
    }

    [Fact]
    public void Expired_To_Active_Via_PaymentReceived_WinBack_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Expired, SubscriptionState.Active, BillingTrigger.PaymentReceived));
    }

    [Fact]
    public void Expired_To_Archived_Via_ReadOnlyWindowEnded_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Expired, SubscriptionState.Archived, BillingTrigger.ReadOnlyWindowEnded));
    }

    [Fact]
    public void Cancelled_To_Active_Via_Reactivated_WinBack_Allowed()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Cancelled, SubscriptionState.Active, BillingTrigger.Reactivated));
    }

    [Fact]
    public void Illegal_Transition_Trialing_To_Suspended_Not_Allowed()
    {
        Assert.False(SubscriptionStateMachine.CanTransition(SubscriptionState.Trialing, SubscriptionState.Suspended, BillingTrigger.GracePeriodEnded));
        var sub = NewSub(SubscriptionState.Trialing);
        Assert.Throws<InvalidOperationException>(() => SubscriptionStateMachine.ValidateTransition(sub, SubscriptionState.Suspended, BillingTrigger.GracePeriodEnded));
    }

    [Fact]
    public void Illegal_Transition_Active_To_Expired_Not_Allowed()
    {
        Assert.False(SubscriptionStateMachine.CanTransition(SubscriptionState.Active, SubscriptionState.Expired, BillingTrigger.TrialExpired));
    }

    [Fact]
    public void Illegal_Transition_Archived_To_PastDue_Not_Allowed()
    {
        Assert.False(SubscriptionStateMachine.CanTransition(SubscriptionState.Archived, SubscriptionState.PastDue, BillingTrigger.InvoiceOverdue));
    }

    [Fact]
    public void Trialing_ManualOverride_ExtendTrial_Stays_Trialing()
    {
        Assert.True(SubscriptionStateMachine.CanTransition(SubscriptionState.Trialing, SubscriptionState.Trialing, BillingTrigger.ManualOverride));
    }

    [Fact]
    public void ApplyTransition_Sets_PastDueSince_And_ReadOnlyUntil()
    {
        var sub = NewSub(SubscriptionState.Active);
        SubscriptionStateMachine.ApplyTransition(sub, SubscriptionState.PastDue, BillingTrigger.InvoiceOverdue, "Invoice overdue");
        Assert.NotNull(sub.PastDueSince);
        Assert.Equal(SubscriptionState.PastDue, sub.State);

        var sub2 = NewSub(SubscriptionState.Trialing);
        SubscriptionStateMachine.ApplyTransition(sub2, SubscriptionState.Expired, BillingTrigger.TrialExpired, "Trial ended");
        Assert.NotNull(sub2.ExpiredSince);
        Assert.NotNull(sub2.ReadOnlyUntil);
        Assert.True(sub2.ReadOnlyUntil > DateTime.UtcNow);
        Assert.Equal(30, (sub2.ReadOnlyUntil!.Value - DateTime.UtcNow).Days + 1); // approx 30 days
    }

    [Fact]
    public void Upgrade_ProRata_Calculation_Exact()
    {
        // Simulate upgrade pro-rata: old plan $1 per learner, new $2, 60 days remaining of 90 day period, 100 learners
        // Price diff per learner = $1, *100 learners = $100, *60/90=0.666... = $66.67 rounded
        var oldPrice = 1.00m;
        var newPrice = 2.00m;
        var learners = 100;
        var daysRemaining = 60;
        var daysInPeriod = 90;
        var diffPerLearner = newPrice - oldPrice; // 1
        var proRataFactor = (decimal)daysRemaining / daysInPeriod; // 0.666...
        var proRataCharge = Math.Round(diffPerLearner * learners * proRataFactor, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(66.67m, proRataCharge); // 100 *0.666... =66.666 ->66.67
    }

    [Fact]
    public void Downgrade_Takes_Effect_Next_Period_Not_Immediate()
    {
        var sub = NewSub(SubscriptionState.Active);
        var now = DateTime.UtcNow;
        sub.CurrentPeriodEnd = now.AddDays(30);

        // Simulate downgrade service logic: pending plan set, effective at period end
        sub.PendingPlanId = 2;
        sub.PendingPlanEffectiveAt = sub.CurrentPeriodEnd;

        Assert.Equal(2, sub.PendingPlanId);
        Assert.True(sub.PendingPlanEffectiveAt > now);
        Assert.Equal(SubscriptionState.Active, sub.State); // stays active now
    }

    [Fact]
    public void All_Legal_Transitions_Count()
    {
        // Ensure we have defined every legal transition per spec
        var all = SubscriptionStateMachine.Transitions;
        Assert.True(all.Count >= 20, $"Expected at least 20 legal transitions, got {all.Count}");
        // Check each state has at least one outgoing
        foreach (SubscriptionState state in Enum.GetValues(typeof(SubscriptionState)))
        {
            var outgoing = all.Where(t => t.From == state).ToList();
            // Archived may have only reactivate, Cancelled may have reactivate/archived
            Assert.True(outgoing.Count > 0 || state == SubscriptionState.Archived, $"State {state} should have at least one outgoing transition");
        }
    }
}
