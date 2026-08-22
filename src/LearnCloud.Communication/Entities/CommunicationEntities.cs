using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Communication.Entities;

// Announcements with audience and expiry date shown across portals
public class Announcement : TenantOwnedEntity
{
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string AudienceType { get; set; } = "all"; // all, class, stream, year_group, role: teacher, parent, student, all_guardians
    public string AudienceFilterJson { get; set; } = "{}"; // gradeId, streamId, role, etc.
    public DateTime? ExpiryDate { get; set; } // shown across portals until expiry
    public string Status { get; set; } = "active"; // active, expired, draft, archived
    public string Priority { get; set; } = "normal"; // low, normal, high, urgent
    public bool ShowInTeacherPortal { get; set; } = true;
    public bool ShowInParentPortal { get; set; } = true;
    public bool ShowInStudentPortal { get; set; } = true;
    public bool ShowInAdminDashboard { get; set; } = true;
    public long CreatedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
}

// Scheduled sending
public class ScheduledMessage : TenantOwnedEntity
{
    public long? BatchId { get; set; } // MessageBatch from messaging module
    public string Title { get; set; } = null!;
    public long? TemplateId { get; set; }
    public string Channel { get; set; } = "sms"; // sms, email
    public string AudienceType { get; set; } = null!;
    public string AudienceFilterJson { get; set; } = "{}";
    public string Body { get; set; } = null!;
    public string? Subject { get; set; }
    public DateTime ScheduledSendAt { get; set; }
    public string Status { get; set; } = "scheduled"; // scheduled, queued, sent, cancelled
    public long CreatedByUserId { get; set; }
}

// Saved audience segments
public class AudienceSegment : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. "Grade 5 Blue Parents", "Arrears > $100"
    public string Description { get; set; } = "";
    public string AudienceType { get; set; } = null!; // class, stream, year_group, all_guardians, arrears_over_x, absent_today, manual, dynamic
    public string FilterJson { get; set; } = "{}"; // gradeId, streamId, arrearsThreshold, etc.
    public bool IsDynamic { get; set; } = false; // true for dynamic lists that re-evaluate at send time (arrears, absent)
    public long CreatedByUserId { get; set; }
    public bool IsSystem { get; set; } = false;
}

// Template library with categories
public class TemplateCategory : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // fees, attendance, academic, general, discipline
    public string Code { get; set; } = null!; // fees, attendance, etc.
    public string? Description { get; set; }
    public string? Color { get; set; } // for UI
}

// Extend MessageTemplate with category (we don't alter existing table, we add via new table linking or new column via migration)
// For V1 we add CategoryId to existing via migration file, but entity here for reference
public class CategorizedTemplate : TenantOwnedEntity
{
    public long TemplateId { get; set; }
    public long CategoryId { get; set; }
}

// Two-way SMS handling if provider supports it
public class InboundSms : TenantOwnedEntity
{
    public string FromNumber { get; set; } = null!; // sender phone
    public string ToNumber { get; set; } = null!; // provider's number / shortcode
    public string Body { get; set; } = null!;
    public string? Provider { get; set; } // EcoCashSms, BulkSmsZw etc.
    public string? ProviderReference { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public long? MatchedGuardianId { get; set; } // matched via phone
    public long? MatchedStudentId { get; set; } // context if message contains student reference
    public string? MatchedSchoolSlug { get; set; }

    public string Status { get; set; } = "received"; // received, matched, unmatched, replied, archived
    public string? ReplyBody { get; set; } // auto-reply or manual reply
    public DateTime? RepliedAt { get; set; }
}

// Event-triggered rule engine (absence for N consecutive days, arrears over threshold, report card published, invoice due in 7 days) with per-tenant config and opt-out
public class CommunicationRule : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. "Absence 3 consecutive days"
    public string Code { get; set; } = null!; // absence_n_days, arrears_over_threshold, report_card_published, invoice_due_7_days
    public string EventType { get; set; } = null!; // same as code
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public string ConfigJson { get; set; } = "{}"; // e.g. {"n":3} for N days, {"threshold":100,"currency":"USD"} for arrears, {"daysBeforeDue":7} for invoice due

    public long? TemplateId { get; set; } // template to use when rule triggers
    public string Channel { get; set; } = "sms"; // sms, email
    public string AudienceType { get; set; } = "dynamic"; // dynamic list based on event

    public bool RespectOptOut { get; set; } = true; // opt-out always honoured
    public bool RespectContactPreferences { get; set; } = true;

    public long CreatedByUserId { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public int TriggerCount { get; set; } = 0;
}

// Per-tenant communication log searchable by learner
public class CommunicationLog : TenantOwnedEntity
{
    public long? BatchId { get; set; } // MessageBatch id
    public long? AnnouncementId { get; set; }
    public long? RuleId { get; set; } // if triggered by rule engine
    public long? InboundSmsId { get; set; } // if two-way related

    public long StudentId { get; set; } // searchable by learner
    public long? GuardianId { get; set; }
    public long? TeacherStaffId { get; set; }

    public string Channel { get; set; } = "sms"; // sms, email, portal, announcement
    public string Direction { get; set; } = "outbound"; // outbound, inbound
    public string RecipientAddress { get; set; } = null!; // phone/email
    public string MessageBody { get; set; } = null!;
    public string? Subject { get; set; }

    public string Status { get; set; } = "sent"; // sent, delivered, failed, read, replied
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public string? FailureReason { get; set; }
    public bool IsOptedOutAtSend { get; set; } = false;
}

// Delivery analytics by campaign
public class CampaignAnalytics
{
    public long BatchId { get; set; }
    public string BatchNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string Channel { get; set; } = "";
    public int TotalRecipients { get; set; }
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Failed { get; set; }
    public int Read { get; set; }
    public int Replied { get; set; }
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = "USD";
    public double DeliveryRate { get; set; } // delivered/sent
    public double ReadRate { get; set; }
    public double ReplyRate { get; set; }
    public List<DailyStat> DailyStats { get; set; } = new();
}

public class DailyStat
{
    public DateTime Date { get; set; }
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Failed { get; set; }
}
