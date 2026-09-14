using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.PlatformAdmin.Entities;

// Support notes per tenant
public class SupportNote : BaseEntity
{
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Content { get; set; } = null!;
    public bool IsInternal { get; set; } = true; // internal only, not visible to school
    public long CreatedByUserId { get; set; } // platform admin user id
    public string? Category { get; set; } // billing, technical, onboarding, etc.
}

// Consented, time-limited support impersonation: school admin grants access, session expires automatically, banner visible, every action recorded as performed-on-behalf-of
// Impersonation without consent must be impossible by design, not by policy - enforced by code: only school admin (tenant) can create grant, platform admin cannot create grant for themselves
public class ImpersonationGrant : BaseEntity
{
    public long TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    // Who granted - must be school admin (tenant role SCHOOL_ADMIN), not platform admin - enforced by service
    public long GrantedByUserId { get; set; } // school admin user id who granted consent
    public string GrantedByRole { get; set; } = "SCHOOL_ADMIN";

    // Who is allowed to impersonate - must be PLATFORM_SUPERADMIN role, but grant itself must be created by tenant admin
    public string GrantedToRole { get; set; } = "PLATFORM_SUPERADMIN";

    public bool HasConsent { get; set; } = true; // explicit consent checkbox
    public string Reason { get; set; } = null!; // why support needed, required
    public DateTime ExpiresAt { get; set; } // e.g. 1 hour from grant
    public DateTime? RevokedAt { get; set; }
    public long? RevokedByUserId { get; set; }

    public bool IsActive => HasConsent && !IsDeleted && ExpiresAt > DateTime.UtcNow && RevokedAt == null;

    public string? TokenHash { get; set; } // hash of grant token for verification, raw token sent to platform admin via secure channel? Actually grant is approval, platform admin then starts session using grant id
}

// Impersonation session - active session when platform admin is impersonating
public class ImpersonationSession : BaseEntity
{
    public long GrantId { get; set; }
    public ImpersonationGrant Grant { get; set; } = null!;

    public long TenantId { get; set; }
    public long ImpersonatorUserId { get; set; } // platform superadmin who is impersonating
    public long? ImpersonatedUserId { get; set; } // optionally which school admin user they impersonate as, or null = as tenant

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } // auto expires, e.g. 1 hour
    public DateTime? EndedAt { get; set; }

    public string? BannerMessage { get; set; } = "You are in support impersonation mode - all actions are audited as performed-on-behalf-of";
    public bool IsActive => !IsDeleted && EndedAt == null && ExpiresAt > DateTime.UtcNow;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

// Announcement broadcast to all school admins for maintenance windows and releases
public class AnnouncementBroadcast : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!; // markdown or html
    public string Audience { get; set; } = "all_school_admins"; // all_school_admins, all_tenants, specific_plan, etc.
    public string? AudienceFilterJson { get; set; } // e.g. {"plan":"growth","state":"active"}

    public DateTime? ScheduledAt { get; set; } // null = immediate
    public DateTime? ExpiresAt { get; set; }

    public string Priority { get; set; } = "normal"; // low, normal, high, urgent for maintenance
    public string Status { get; set; } = "draft"; // draft, scheduled, sent, archived

    public long CreatedByUserId { get; set; } // platform admin
    public DateTime? SentAt { get; set; }
    public int SentCount { get; set; } // how many tenants notified
}

// For business metrics and operational views - not necessarily tables, but we can have materialized views or cached aggregates
public class TenantHealthScore : BaseEntity
{
    public long TenantId { get; set; }
    public int Score { get; set; } // 0-100
    public string HealthStatus { get; set; } = "healthy"; // healthy, warning, at_risk, critical
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public string? FactorsJson { get; set; } // JSON of factors: falling logins, rising tickets, unpaid, over limit, etc.
    public decimal MonthlyValue { get; set; } // MRR contribution
    public DateTime? LastActivityAt { get; set; }
}

// Background job status and failures - operational view
public class BackgroundJobRecord : BaseEntity
{
    public long? TenantId { get; set; } // null for platform jobs
    public string JobType { get; set; } = null!; // e.g. invoice_generation, dunning, messaging_batch, promotion_batch, backup
    public string? JobId { get; set; } // Hangfire job id or similar
    public string Status { get; set; } = "pending"; // pending, running, completed, failed, cancelled
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultJson { get; set; }
    public long? CreatedByUserId { get; set; }
}

// Error rates - aggregated from logs or Sentry
public class ErrorRateSnapshot : BaseEntity
{
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow.Date;
    public long? TenantId { get; set; } // null for platform-wide
    public string Service { get; set; } = "api"; // api, web, worker
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int RequestCount { get; set; }
    public double ErrorRate { get; set; } // errorCount / requestCount
}

// SMS spend by tenant - operational view, extends TenantMessagingUsage but aggregated
public class SmsSpendRecord : BaseEntity
{
    public long TenantId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int SmsCount { get; set; }
    public decimal SmsCost { get; set; }
    public decimal EmailCount { get; set; }
    public decimal EmailCost { get; set; }
    public string Currency { get; set; } = "USD";
}

// Storage growth per tenant
public class StorageGrowthRecord : BaseEntity
{
    public long TenantId { get; set; }
    public long TotalFiles { get; set; }
    public long TotalBytes { get; set; } // sum file sizes
    public long DocumentBytes { get; set; } // student documents
    public long PhotoBytes { get; set; } // photos
    public long BackupBytes { get; set; }
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
}
