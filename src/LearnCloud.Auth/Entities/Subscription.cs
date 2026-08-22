namespace LearnCloud.Auth.Entities;

public class SubscriptionPlan : BaseEntity
{
    public new long? TenantId { get; set; } // global, null
    public string Code { get; set; } = null!; // starter, growth, scale
    public string Name { get; set; } = null!;
    public int MaxLearners { get; set; }
    public decimal PriceMonthly { get; set; } // DECIMAL(18,2)
    public decimal PriceAnnual { get; set; }
    public string Currency { get; set; } = "USD";
    public string? FeaturesJson { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TenantSubscription : BaseEntity
{
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public long PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public string BillingCycle { get; set; } = "monthly";
    public string Status { get; set; } = "trialing"; // trialing, active, past_due, cancelled
    public DateTime? TrialEndsAt { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public int MeteredActiveStudents { get; set; } = 0;
    public bool OverLimitFlag { get; set; } = false;
    public string Currency { get; set; } = "USD";
}
