using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.ParentPortal.Entities;

// Invitation flow the school triggers
public class GuardianInvitation : TenantOwnedEntity
{
    public long GuardianId { get; set; }
    public string Email { get; set; } = null!; // invited email
    public string? Phone { get; set; }
    public string TokenHash { get; set; } = null!; // SHA256 hashed single-use
    public DateTime ExpiresAt { get; set; } // 7 days
    public DateTime? UsedAt { get; set; }
    public bool IsUsed => UsedAt.HasValue;
    public long CreatedByUserId { get; set; } // registrar/school admin who triggered
    public string? InvitationLink { get; set; } // for logging (not storing raw token)
}

// Parent message to class teacher, school-side moderation and rate limiting
public class ParentTeacherMessage : TenantOwnedEntity
{
    public long GuardianId { get; set; }
    public long StudentId { get; set; } // context which child
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public long SenderUserId { get; set; } // guardian user
    public long? RecipientTeacherStaffId { get; set; } // class teacher or subject teacher
    public long? RecipientUserId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = null!;
    public string Status { get; set; } = "pending"; // pending, approved, rejected, sent
    public bool RequiresModeration { get; set; } = false;
    public long? ModeratedByUserId { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? ModerationNote { get; set; }
    public string? RateLimitKey { get; set; } // guardianId + class
}

// School setting for parent-teacher messaging
public class ParentMessagingSettings : TenantOwnedEntity
{
    public bool EnableParentTeacherMessaging { get; set; } = true;
    public bool RequireModeration { get; set; } = false; // school-side moderation setting
    public int RateLimitPerHour { get; set; } = 5; // rate limiting: 5 messages per hour per guardian per class
    public int RateLimitPerDay { get; set; } = 20;
    public string? AllowedRoles { get; set; } // JSON list of roles allowed to receive parent messages, e.g. ["TEACHER"]
}

// For home screen aggregates - not a table, but we can cache
public class ParentHomeCache
{
    public long StudentId { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal AttendancePercentage { get; set; }
    public string? LatestResultSummary { get; set; }
    public int UpcomingAssessmentsCount { get; set; }
    public int RecentNoticesCount { get; set; }
    public int HomeworkDueCount { get; set; }
}
