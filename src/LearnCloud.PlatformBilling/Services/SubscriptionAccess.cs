using LearnCloud.Infrastructure.Jobs;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Jobs;

namespace LearnCloud.PlatformBilling.Services;

/// <summary>Billing settings, section "Billing".</summary>
public sealed class BillingOptions
{
    /// <summary>
    /// Blocks changes (not reading) for schools whose subscription is suspended, expired,
    /// cancelled or archived. Turn off to let every school keep editing, e.g. before payments
    /// are set up.
    /// </summary>
    public bool EnforceReadOnly { get; set; } = true;

    /// <summary>Where schools are told to go to pay or reactivate.</summary>
    public string ContactEmail { get; set; } = "billing@learncloud.co.zw";
}

/// <summary>The one definition of what a subscription state allows.</summary>
public static class SubscriptionAccess
{
    /// <summary>
    /// Read-only: the school can view and export its records but not change them. Past due is
    /// a grace period and stays writable; data is never deleted and reading is never blocked.
    /// </summary>
    public static bool IsReadOnly(SubscriptionState state) => state is
        SubscriptionState.Suspended or SubscriptionState.Expired or SubscriptionState.Cancelled or SubscriptionState.Archived;

    public static string Banner(Subscription sub, string contactEmail) => sub.State switch
    {
        SubscriptionState.Suspended => $"Your account is suspended for non-payment since {sub.SuspendedSince:yyyy-MM-dd}. You can view and export records but not change them. Contact {contactEmail} to pay and reactivate.",
        SubscriptionState.Expired => $"Your trial ended on {sub.TrialEndsAt:yyyy-MM-dd}. Your school is read-only until {sub.ReadOnlyUntil:yyyy-MM-dd}. Contact {contactEmail} to subscribe and keep working.",
        SubscriptionState.Cancelled => $"Your subscription is cancelled. Your records are kept and can be viewed. Contact {contactEmail} to reactivate.",
        SubscriptionState.Archived => $"Your account is archived. Your records are kept. Contact {contactEmail} to reactivate.",
        SubscriptionState.PastDue => $"Your subscription is past due since {sub.PastDueSince:yyyy-MM-dd}. Please pay within {sub.PastDueGraceDays} days of that date to avoid suspension. Contact {contactEmail}.",
        SubscriptionState.Trialing when sub.TrialEndsAt is DateTime end => $"Trial: {Math.Max(0, (end - DateTime.UtcNow).Days)} days left. No card required.",
        _ => "",
    };
}

/// <summary>Daily trial reminders, overdue handling, suspension and archival for all schools.</summary>
public sealed class DunningScheduledJob : IScheduledJob
{
    public string Name => "billing-dunning";
    public TimeSpan Interval => TimeSpan.FromDays(1);
    public TimeSpan FirstRunDelay => TimeSpan.FromMinutes(5);

    public Task RunAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<DunningJob>().RunAsync(ct);
}
