namespace LearnCloud.Communication.DTOs;

// Announcements
public record CreateAnnouncementRequest(string Title, string Body, string AudienceType, string AudienceFilterJson, DateTime? ExpiryDate, string Priority, bool ShowInTeacherPortal, bool ShowInParentPortal, bool ShowInStudentPortal, bool ShowInAdminDashboard);
public record AnnouncementDto(long Id, string Title, string Body, string AudienceType, string AudienceFilterJson, DateTime? ExpiryDate, string Status, string Priority, bool ShowTeacher, bool ShowParent, bool ShowStudent, bool ShowAdmin, DateTime CreatedAt, long CreatedByUserId);
public record UpdateAnnouncementRequest(string? Title, string? Body, DateTime? ExpiryDate, string? Status);

// Scheduled sending
public record CreateScheduledMessageRequest(string Title, long? TemplateId, string Channel, string AudienceType, string AudienceFilterJson, string Body, string? Subject, DateTime ScheduledSendAt);
public record ScheduledMessageDto(long Id, string Title, long? TemplateId, string Channel, string AudienceType, string Body, string? Subject, DateTime ScheduledSendAt, string Status, long CreatedByUserId, DateTime CreatedAt);

// Saved audience segments
public record CreateAudienceSegmentRequest(string Name, string Description, string AudienceType, string FilterJson, bool IsDynamic);
public record AudienceSegmentDto(long Id, string Name, string Description, string AudienceType, string FilterJson, bool IsDynamic, long CreatedByUserId, bool IsSystem, DateTime CreatedAt);
public record PreviewSegmentRequest(long? SegmentId, string AudienceType, string FilterJson);
public record PreviewSegmentResponse(int TotalGuardians, int TotalStudents, List<SampleRecipientDto> SampleRecipients);
public record SampleRecipientDto(long GuardianId, string GuardianName, string Phone, long? StudentId, string? StudentName);

// Template library with categories
public record TemplateCategoryDto(long Id, string Name, string Code, string? Description, string? Color);
public record CreateTemplateCategoryRequest(string Name, string Code, string? Description, string? Color);
public record TemplateWithCategoryDto(long Id, string Name, string Code, string Channel, string Subject, string Body, bool IsSystem, string? CategoryName, string? CategoryCode, List<string> MergeFields);

// Two-way SMS
public record InboundSmsDto(long Id, string FromNumber, string ToNumber, string Body, string? Provider, string? ProviderReference, DateTime ReceivedAt, long? MatchedGuardianId, long? MatchedStudentId, string Status, string? ReplyBody, DateTime? RepliedAt);
public record ReplyInboundSmsRequest(long InboundSmsId, string ReplyBody);
public record WebhookInboundSmsRequest(string From, string To, string Text, string? ProviderReference, string? Provider);

// Event-triggered rule engine
public record CreateRuleRequest(string Name, string Code, string EventType, string Description, bool IsActive, string ConfigJson, long? TemplateId, string Channel, string AudienceType, bool RespectOptOut);
public record RuleDto(long Id, string Name, string Code, string EventType, string Description, bool IsActive, string ConfigJson, long? TemplateId, string Channel, string AudienceType, bool RespectOptOut, DateTime? LastTriggeredAt, int TriggerCount, DateTime CreatedAt);
public record UpdateRuleRequest(bool? IsActive, string? ConfigJson, long? TemplateId, string? Channel);
public record TriggerRuleRequest(long? StudentId, long? GradeId, long? StreamId, decimal? Amount, DateTime? Date, string? ExtraJson); // manual trigger for testing

// Delivery analytics by campaign
public record CampaignAnalyticsDto(long BatchId, string BatchNumber, string Title, string Channel, int TotalRecipients, int Sent, int Delivered, int Failed, int Read, int Replied, decimal TotalCost, string Currency, double DeliveryRate, double ReadRate, double ReplyRate, List<DailyStatDto> DailyStats);
public record DailyStatDto(DateTime Date, int Sent, int Delivered, int Failed);

// Per-tenant communication log searchable by learner
public record CommunicationLogDto(long Id, long? BatchId, long? AnnouncementId, long? RuleId, long StudentId, string StudentName, long? GuardianId, string GuardianName, string Channel, string Direction, string RecipientAddress, string MessageBody, string? Subject, string Status, string? Provider, decimal Cost, string Currency, DateTime SentAt, DateTime? DeliveredAt, DateTime? ReadAt, string? FailureReason);
public record CommunicationLogSearchRequest(long? StudentId, long? GuardianId, string? Channel, string? Status, DateTime? FromDate, DateTime? ToDate, string? SearchText, int Page = 1, int PageSize = 50);
public record PagedCommunicationLogDto(List<CommunicationLogDto> Items, int Total, int Page, int PageSize);
