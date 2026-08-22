using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.AI.Entities;

// AI Provider settings per tenant - swappable without touching calling code
public class AIProviderSettings : TenantOwnedEntity
{
    public string ProviderName { get; set; } = "RuleBased"; // RuleBased, OpenAI, Claude, Gemini
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public string? ConfigJson { get; set; } // {apiKey, model, temperature, maxTokens}
    public bool EnableCommentDrafting { get; set; } = true;
    public bool EnableAttendanceAnomaly { get; set; } = true;
    public bool EnableAtRiskDetection { get; set; } = true;
}

// 1. Report card comment drafting: given learner's marks, attendance and subject performance, draft teacher comment
public class ReportCommentDraft : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long? ReportCardId { get; set; }

    public string InputDataJson { get; set; } = null!; // snapshot of marks, attendance, subject performance
    public string DraftComment { get; set; } = null!; // AI generated draft
    public string Tone { get; set; } = "encouraging"; // encouraging, formal, concise, detailed, neutral
    public string Length { get; set; } = "medium"; // short, medium, long

    public string? EditedComment { get; set; } // teacher reviewed and edited
    public bool IsEdited { get; set; } = false;
    public bool IsSaved { get; set; } = false; // teacher always reviews and edits before saving; nothing written automatically
    public long? EditedByUserId { get; set; }
    public DateTime? EditedAt { get; set; }
    public long? SavedByUserId { get; set; }
    public DateTime? SavedAt { get; set; }

    public string? ProviderName { get; set; } // which AI provider generated
    public string? Model { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public decimal? Cost { get; set; }
}

// 2. Attendance anomaly detection: flag unusual patterns
public class AttendanceAnomaly : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public string AnomalyType { get; set; } = null!; // weekday_pattern, sudden_drop, consecutive_absence, low_attendance
    public string Description { get; set; } = null!; // human readable why flagged
    public string Explanation { get; set; } = null!; // detailed explanation of why flagged
    public decimal ConfidenceScore { get; set; } // 0-100
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string? DataJson { get; set; } // supporting data e.g. {"weekday":"Monday","missed":4,"total":5,"rate":80}
    public string Status { get; set; } = "new"; // new, acknowledged, resolved, dismissed
    public long? AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

// 3. At-risk learner identification: combine falling marks, declining attendance and fee arrears
public class AtRiskFlag : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public string RiskLevel { get; set; } = "medium"; // low, medium, high, critical
    public decimal RiskScore { get; set; } // 0-100
    public string FlagReason { get; set; } = null!; // summary
    public string UnderlyingReasonsJson { get; set; } = null!; // JSON array of reasons with details: [{"type":"falling_marks","detail":"Math dropped 15% from 80% to 65% Term1->Term2","severity":"high"}, ...]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string Status { get; set; } = "new"; // new, in_follow_up, resolved, dismissed
    public long? AssignedToUserId { get; set; } // pastoral staff assigned
    public string? FollowUpNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
