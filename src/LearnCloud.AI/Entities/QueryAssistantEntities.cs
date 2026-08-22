using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.AI.Entities;

// Settings screen where a school opts in - explicit per-tenant opt-in, school must be told plainly what is sent and to which provider
public class AIQueryOptInSettings : TenantOwnedEntity
{
    public bool IsOptedIn { get; set; } = false; // explicit per-tenant opt-in required, default false = no personal data leaves system
    public DateTime? OptedInAt { get; set; }
    public long? OptedInByUserId { get; set; }
    public string? OptedInByRole { get; set; }

    public bool AllowPersonalLearnerDataToLeave { get; set; } = false; // separate flag for personal learner data - stricter
    public bool HasAcknowledgedWhatIsSent { get; set; } = false; // school must be told plainly what is sent and to which provider, checkbox
    public string AcknowledgedNoticeText { get; set; } = ""; // snapshot of notice they acknowledged

    public string? ProviderName { get; set; } // which provider they opted in to: RuleBased (local, no data leaves), OpenAI, Claude
    public string? ProviderDescription { get; set; } // plain language what is sent and to which provider

    public bool EnableQueryAssistant { get; set; } = false; // feature toggle

    public string? OptOutReason { get; set; }
    public DateTime? OptedOutAt { get; set; }
}

// Provider access behind abstraction with cost tracking and per-tenant cap
public class AIQueryProviderSettings : TenantOwnedEntity
{
    public string ProviderName { get; set; } = "RuleBased"; // RuleBased (local, no data leaves), OpenAI, Claude
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public string? ConfigJson { get; set; } // {apiKey, model, temperature} encrypted

    public decimal CostPer1000TokensInput { get; set; } = 0.005m;
    public decimal CostPer1000TokensOutput { get; set; } = 0.015m;
    public decimal MonthlyCapUsd { get; set; } = 10.00m; // per-tenant cap
    public decimal CurrentMonthCost { get; set; } = 0m;
    public int CurrentMonthYear { get; set; } = DateTime.UtcNow.Year;
    public int CurrentMonthMonth { get; set; } = DateTime.UtcNow.Month;
}

// Every AI feature is assistive - human approves before anything saved or sent - log
public class AIQueryLog : TenantOwnedEntity
{
    public long QueriedByUserId { get; set; }
    public string NaturalLanguageQuery { get; set; } = null!; // e.g. "How many students have arrears over $100 in Grade 5?"
    public string InterpretedQueryJson { get; set; } = null!; // structured interpretation
    public string SqlQuery { get; set; } = null!; // read-only SELECT with tenant_id filter
    public string InputsJson { get; set; } = null!; // inputs and reasoning shown
    public string Reasoning { get; set; } = null!; // explanation of how query was interpreted, tentatively phrased
    public string ResultJson { get; set; } = null!; // results (aggregated, no personal data unless opt-in)
    public int ResultCount { get; set; }
    public string Status { get; set; } = "pending_review"; // pending_review, approved, dismissed, saved, sent

    public bool IsDismissed { get; set; } = false;
    public DateTime? DismissedAt { get; set; }
    public long? DismissedByUserId { get; set; }

    public bool IsApproved { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
    public long? ApprovedByUserId { get; set; }

    public string? ProviderName { get; set; }
    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public decimal? Cost { get; set; }

    public string? TentativeLanguageNotice { get; set; } = "This is an AI-assisted suggestion, not a fact. Please review and verify before acting. Predictions about a child are tentative and oriented toward support, never labelling.";
}

// For cost tracking per-tenant cap
public class AIQueryUsage : TenantOwnedEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int QueryCount { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = "USD";
}
