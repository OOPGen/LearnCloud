namespace LearnCloud.PlatformAdmin.DTOs;

// Tenant list: school, plan, subscription state, learner count, last activity, monthly value, health indicator
public record TenantListDto(
    long TenantId,
    string SchoolName,
    string Slug,
    string City,
    string PlanName,
    string PlanCode,
    string SubscriptionState,
    int LearnerCount,
    int LearnerLimit,
    bool IsOverLimit,
    DateTime? LastActivityAt,
    decimal MonthlyValue,
    string Currency,
    string HealthStatus,
    int HealthScore,
    DateTime? TrialEndsAt,
    DateTime CurrentPeriodEnd
);

public record TenantListRequest(string? State, string? PlanCode, string? Search, string? HealthStatus, string? SortBy, bool SortDesc, int Page = 1, int PageSize = 50);

// Tenant detail: subscription history, invoices, usage, support notes, manual actions
public record TenantDetailDto(
    long TenantId,
    string SchoolName,
    string Slug,
    string City,
    string ContactEmail,
    string ContactPhone,
    string PrimaryColor,
    DateTime CreatedAt,
    SubscriptionDto? CurrentSubscription,
    List<SubscriptionHistoryDto> SubscriptionHistory,
    List<InvoiceDto> PlatformInvoices,
    UsageDto Usage,
    List<SupportNoteDto> SupportNotes,
    HealthDto Health
);

public record SubscriptionDto(long Id, string PlanName, string PlanCode, string State, string PreviousState, DateTime? TrialStartedAt, DateTime? TrialEndsAt, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd, int BillableLearnerCount, int CurrentLearnerCount, DateTime? PastDueSince, DateTime? SuspendedSince, DateTime? ExpiredSince, DateTime? ReadOnlyUntil, long? PendingPlanId, DateTime? PendingPlanEffectiveAt, string Currency);
public record SubscriptionHistoryDto(long Id, string FromState, string ToState, string Trigger, string? Reason, DateTime CreatedAt, long? ActorUserId);
public record InvoiceDto(long Id, string InvoiceNumber, DateTime IssueDate, DateTime DueDate, decimal TotalAmount, decimal AmountPaid, decimal BalanceDue, string Currency, string Status, string? Notes);
public record UsageDto(int UsersCount, int LearnersCount, int SmsCount, decimal SmsCost, int EmailCount, long StorageBytes, decimal StorageGb, List<MonthlyUsageDto> MonthlyTrend);
public record MonthlyUsageDto(int Year, int Month, int Learners, int Users, int Sms, decimal SmsCost, long StorageBytes);
public record SupportNoteDto(long Id, string Content, bool IsInternal, string? Category, long CreatedByUserId, string CreatedByName, DateTime CreatedAt);
public record HealthDto(int Score, string Status, DateTime CalculatedAt, string? FactorsJson, decimal MonthlyValue);

public record CreateSupportNoteRequest(string Content, bool IsInternal, string? Category);

// Manual actions (extend trial, change plan, credit invoice, suspend, reactivate) each requiring reason
public record ExtendTrialRequest(DateTime NewTrialEndsAt, string Reason);
public record ChangePlanRequest(long NewPlanId, string Reason, bool IsUpgrade);
public record CreditInvoiceRequest(long InvoiceId, decimal Amount, string Reason);
public record SuspendTenantRequest(string Reason, bool IsImmediate);
public record ReactivateTenantRequest(string Reason);

// Consented, time-limited support impersonation
public record GrantImpersonationRequest(string Reason, int DurationMinutes, bool HasConsent);
public record ImpersonationGrantDto(long Id, long TenantId, string TenantName, long GrantedByUserId, string GrantedByName, string GrantedByRole, string GrantedToRole, bool HasConsent, string Reason, DateTime ExpiresAt, DateTime? RevokedAt, bool IsActive, DateTime CreatedAt);
public record StartImpersonationRequest(long GrantId);
public record ImpersonationSessionDto(long Id, long GrantId, long TenantId, string TenantName, long ImpersonatorUserId, string ImpersonatorName, long? ImpersonatedUserId, DateTime StartedAt, DateTime ExpiresAt, DateTime? EndedAt, bool IsActive, string BannerMessage, string? IpAddress);

// Business metrics
public record BusinessMetricsDto(
    decimal Mrr,
    decimal PreviousMrr,
    decimal MrrGrowth,
    int NewTenantsThisMonth,
    int NewTenantsLastMonth,
    int ChurnThisMonth,
    decimal ChurnRate,
    double TrialConversionRate,
    List<RevenueByPlanDto> RevenueByPlan,
    int TrialsTotal,
    int TrialsConverting,
    int TrialsAtRisk,
    List<TenantAtRiskDto> SchoolsAtRisk,
    List<MonthlyRevenueDto> RevenueByMonth
);

public record RevenueByPlanDto(string PlanName, string PlanCode, int TenantsCount, decimal MonthlyRevenue, decimal Percentage);
public record MonthlyRevenueDto(int Year, int Month, string MonthName, decimal Revenue, int NewTenants, int ChurnedTenants, int ActiveTenants);
public record TenantAtRiskDto(long TenantId, string SchoolName, string Slug, string State, string PlanName, int Billable, int Current, int Limit, bool OverLimit, DateTime? PastDueSince, DateTime? SuspendedSince, string Reason, int HealthScore, DateTime? LastActivityAt);

// Operational views
public record BackgroundJobStatusDto(long Id, long? TenantId, string? TenantName, string JobType, string? JobId, string Status, DateTime? StartedAt, DateTime? CompletedAt, int RetryCount, string? ErrorMessage, DateTime CreatedAt);
public record ErrorRateDto(DateTime SnapshotDate, long? TenantId, string Service, int ErrorCount, int WarningCount, int RequestCount, double ErrorRate);
public record SmsSpendByTenantDto(long TenantId, string SchoolName, int Year, int Month, int SmsCount, decimal SmsCost, int EmailCount, decimal EmailCost, string Currency, decimal TotalCost);
public record StorageGrowthDto(long TenantId, string SchoolName, long TotalFiles, long TotalBytes, decimal TotalGb, long DocumentBytes, long PhotoBytes, DateTime MeasuredAt, decimal GrowthLast30DaysGb);

// Announcement broadcast
public record CreateBroadcastRequest(string Title, string Body, string Audience, string? AudienceFilterJson, DateTime? ScheduledAt, DateTime? ExpiresAt, string Priority);
public record BroadcastDto(long Id, string Title, string Body, string Audience, string? AudienceFilterJson, DateTime? ScheduledAt, DateTime? ExpiresAt, string Priority, string Status, long CreatedByUserId, DateTime? SentAt, int SentCount, DateTime CreatedAt);

// Second factor
public record SecondFactorSetupDto(string QrCodeUri, string Secret, string[] RecoveryCodes);
public record VerifySecondFactorRequest(string Code, string? RecoveryCode);
