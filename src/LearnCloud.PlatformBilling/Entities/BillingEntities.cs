using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.PlatformBilling.Entities;

public enum SubscriptionState
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Suspended = 4,
    Cancelled = 5,
    Expired = 6,
    Archived = 7 // after 30-day read-only window
}

public enum BillingTrigger
{
    TrialStarted,
    TrialConverted,
    PaymentReceived,
    InvoiceOverdue,
    GracePeriodEnded,
    SuspensionGraceEnded,
    TrialExpired,
    ReadOnlyWindowEnded,
    CancelledBySchool,
    CancelledByAdmin,
    PlanUpgraded,
    PlanDowngraded,
    ManualOverride,
    Reactivated
}

// Plan: name, description, price per learner per term, minimum charge, included modules as feature flags, included SMS bundle, learner limit
public class Plan : BaseEntity
{
    public string Name { get; set; } = null!; // Starter, Growth, Scale
    public string Code { get; set; } = null!; // starter, growth, scale
    public string Description { get; set; } = null!;
    public decimal PricePerLearnerPerTerm { get; set; } // e.g. 2.00 USD per learner per term
    public decimal MinimumCharge { get; set; } // e.g. 99 minimum even if 10 learners
    public int LearnerLimit { get; set; } // max learners, e.g. 300, 800, 2000
    public int IncludedSmsBundle { get; set; } // e.g. 500 SMS per term included
    public int IncludedEmailBundle { get; set; } = 2000;
    public string IncludedModulesJson { get; set; } = "[]"; // feature flags ["students","attendance","fees","assessments","messaging","timetable","reports"]
    public string FeaturesJson { get; set; } = "{}"; // extra flags
    public bool IsActive { get; set; } = true;
    public string Currency { get; set; } = "USD";
    public bool IsTrialPlan { get; set; } = false;
}

// Subscription per tenant with state machine
public class Subscription : BaseEntity
{
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public long PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public SubscriptionState State { get; set; } = SubscriptionState.Trialing;
    public string PreviousState { get; set; } = "";

    // Trial
    public DateTime? TrialStartedAt { get; set; }
    public DateTime? TrialEndsAt { get; set; } // 14 days after start, no card
    public DateTime? TrialConvertedAt { get; set; }

    // Billing period aligned to school term, enrolment snapshot at period start used as billable count
    public long? AcademicYearId { get; set; }
    public long? TermId { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public int BillableLearnerCount { get; set; } // enrolment snapshot at period start
    public int CurrentLearnerCount { get; set; } // live count for over-limit warning

    // Grace periods configurable
    public int PastDueGraceDays { get; set; } = 7; // after due date -> past_due, then after 7 days -> suspended
    public int SuspensionGraceDays { get; set; } = 30; // suspended -> cancelled after 30 days

    public DateTime? PastDueSince { get; set; }
    public DateTime? SuspendedSince { get; set; }
    public DateTime? ExpiredSince { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Read-only window after expiry before archival: 30 days
    public DateTime? ReadOnlyUntil { get; set; }

    // For plan changes: upgrade immediate with pro-rata, downgrade next period
    public long? PendingPlanId { get; set; } // for downgrade at next period
    public DateTime? PendingPlanEffectiveAt { get; set; }

    public string Currency { get; set; } = "USD";
}

// Platform invoices to the school, with numbering, line items, due date and payment recording
public class PlatformInvoice : BaseEntity
{
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public long SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    public string InvoiceNumber { get; set; } = null!; // PLAT-2026-T2-00001
    public long? AcademicYearId { get; set; }
    public long? TermId { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }

    public decimal Subtotal { get; set; } // billable * price per learner
    public decimal MinimumChargeApplied { get; set; } // minimum charge if applicable
    public decimal ProRataAdjustment { get; set; } // for upgrade immediate
    public decimal DiscountAmount { get; set; } // trial credit or manual credit
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "draft"; // draft, issued, partial, paid, void, credited

    public string? Notes { get; set; }

    public ICollection<PlatformInvoiceLine> Lines { get; set; } = new List<PlatformInvoiceLine>();
    public ICollection<PlatformPayment> Payments { get; set; } = new List<PlatformPayment>();
}

public class PlatformInvoiceLine : BaseEntity
{
    public long InvoiceId { get; set; }
    public PlatformInvoice Invoice { get; set; } = null!;
    public long TenantId { get; set; }

    public string Description { get; set; } = null!; // e.g. "Growth plan - 542 learners @ $2.00/learner - Term 2 2026 (billable 542)"
    public int Quantity { get; set; } = 1; // learner count
    public decimal UnitPrice { get; set; } // price per learner
    public decimal LineTotal { get; set; } // quantity * unit price
    public string Currency { get; set; } = "USD";
    public string? MetadataJson { get; set; } // e.g. billable snapshot, plan code
}

// Platform payment recording (manual capture at first, gateway later)
public class PlatformPayment : BaseEntity
{
    public long TenantId { get; set; }
    public long InvoiceId { get; set; }
    public PlatformInvoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Method { get; set; } = "manual"; // manual, paynow, stripe, bank_transfer
    public string? Reference { get; set; } // gateway ref
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "confirmed"; // confirmed, pending, reversed
}

// Dunning history - escalating reminders
public class DunningEvent : BaseEntity
{
    public long TenantId { get; set; }
    public long SubscriptionId { get; set; }
    public long? InvoiceId { get; set; }

    public string EventType { get; set; } = null!; // trial_reminder_day_7, trial_reminder_day_12, trial_expired, invoice_overdue, past_due, suspension_warning, suspended, expiry_readonly, archived
    public string Channel { get; set; } = "email"; // email, sms, portal_banner
    public string Recipient { get; set; } = null!; // email
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; } = true;
    public string? FailureReason { get; set; }
}

// Plan change log - upgrade immediate with pro-rata, downgrade next period, both logged
public class PlanChangeLog : BaseEntity
{
    public long TenantId { get; set; }
    public long SubscriptionId { get; set; }
    public long FromPlanId { get; set; }
    public long ToPlanId { get; set; }
    public string ChangeType { get; set; } = null!; // upgrade, downgrade
    public string EffectiveType { get; set; } = null!; // immediate, next_period
    public DateTime EffectiveAt { get; set; }
    public decimal ProRataCharge { get; set; } // for upgrade immediate
    public string? Reason { get; set; }
    public long ChangedByUserId { get; set; }
    public string? Notes { get; set; }
}

// Manual override to extend trial or credit invoice, fully audited
public class BillingOverride : BaseEntity
{
    public long TenantId { get; set; }
    public long? SubscriptionId { get; set; }
    public long? InvoiceId { get; set; }
    public string OverrideType { get; set; } = null!; // extend_trial, credit_invoice, extend_suspension_grace, change_plan
    public string DetailsJson { get; set; } = "{}"; // e.g. new trial_ends_at, credit amount
    public string Reason { get; set; } = null!; // required, audited
    public long AdminUserId { get; set; }
    public DateTime CreatedAtOverride { get; set; } = DateTime.UtcNow;
}

// Feature gating - included modules as feature flags
public static class FeatureFlags
{
    public const string Students = "students";
    public const string Guardians = "guardians";
    public const string Staff = "staff";
    public const string Attendance = "attendance";
    public const string Timetable = "timetable";
    public const string Fees = "fees";
    public const string Assessments = "assessments";
    public const string ReportCards = "report_cards";
    public const string Messaging = "messaging";
    public const string Reports = "reports";
    public const string Settings = "settings";
    public const string Academic = "academic";
    public const string Admissions = "admissions";

    public static readonly string[] All = new[]
    {
        Students, Guardians, Staff, Attendance, Timetable, Fees, Assessments, ReportCards, Messaging, Reports, Settings, Academic, Admissions
    };
}
