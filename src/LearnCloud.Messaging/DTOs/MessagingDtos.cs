using LearnCloud.Messaging.Entities;

namespace LearnCloud.Messaging.DTOs;

public record CreateTemplateRequest(
    string Name,
    string Code,
    string Channel, // sms, email
    string Subject,
    string Body,
    string? Description,
    bool IsSystem = false
);

public record TemplateDto(
    long Id,
    string Name,
    string Code,
    string Channel,
    string Subject,
    string Body,
    bool IsSystem,
    string? Description,
    List<string> MergeFields
);

public record AudienceRequest(
    string Type, // class, stream, year_group, all_guardians, arrears_over_x, absent_today, manual
    long? GradeId,
    long? StreamId,
    long? AcademicYearId,
    decimal? ArrearsThreshold,
    DateTime? Date, // for absent_today
    List<long>? ManualGuardianIds,
    List<long>? ManualStudentIds // for dynamic: guardians of these students
);

public record CreateMessageBatchRequest(
    string Title,
    long? TemplateId,
    string Channel, // sms, email
    AudienceRequest Audience,
    string? CustomBody, // if not using template, or override
    string? CustomSubject
);

public record PreviewRequest(
    long? TemplateId,
    string Channel,
    AudienceRequest Audience,
    string Body,
    string? Subject,
    long? SampleGuardianId // preview against real recipient
);

public record PreviewResponse(
    int TotalRecipients,
    int FilteredOptOut,
    int FilteredNoContact,
    string SampleRecipientName,
    string SampleRecipientAddress,
    string RenderedSubject,
    string RenderedBody,
    List<string> MergeFieldsUsed,
    CostEstimateDto CostEstimate
);

public record CostEstimateDto(
    int TotalRecipients,
    int SmsRecipients,
    int EmailRecipients,
    int FilteredOutOptOut,
    int FilteredOutNoContact,
    decimal CostPerSms,
    decimal CostPerEmail,
    decimal TotalCost,
    string Currency,
    bool CapWarning,
    string? CapWarningMessage,
    bool CapExceeded,
    int RemainingAfterSend
);

public record MessageBatchDto(
    long Id,
    string BatchNumber,
    string Title,
    long? TemplateId,
    string Channel,
    string AudienceType,
    string Body,
    string? Subject,
    int TotalRecipients,
    int SentCount,
    int DeliveredCount,
    int FailedCount,
    decimal EstimatedCost,
    decimal ActualCost,
    string Currency,
    string Status,
    DateTime? QueuedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? CostEstimateJson,
    DateTime CreatedAt
);

public record DeliveryLogDto(
    long Id,
    long BatchId,
    long? GuardianId,
    long? StudentId,
    string RecipientName,
    string RecipientAddress,
    string Channel,
    string Status,
    string? Provider,
    string? ProviderReference,
    decimal Cost,
    string Currency,
    string RenderedBody,
    int RetryCount,
    DateTime? LastAttemptAt,
    DateTime? DeliveredAt,
    string? FailureReason,
    bool IsOptedOut
);

public record UsageDto(
    int Year,
    int Month,
    int SmsCount,
    int EmailCount,
    decimal SmsCost,
    decimal EmailCost,
    int SmsLimit,
    int EmailLimit,
    int SmsRemaining,
    int EmailRemaining,
    bool NearCap,
    string? WarningMessage
);

public record OptOutRequest(long GuardianId, string Channel, string? Reason);
public record ContactPreferenceDto(long GuardianId, bool SmsOptIn, bool EmailOptIn, bool SmsOptOut, bool EmailOptOut, string? PreferredLanguage);
