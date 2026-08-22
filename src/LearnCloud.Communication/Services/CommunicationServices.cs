using LearnCloud.Communication.DTOs;
using LearnCloud.Communication.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Communication.Services;

public interface IAnnouncementService
{
    Task<AnnouncementDto> CreateAsync(long tenantId, long userId, CreateAnnouncementRequest req, CancellationToken ct = default);
    Task<List<AnnouncementDto>> ListActiveAsync(long tenantId, string portal, CancellationToken ct = default); // portal = teacher, parent, student, admin - shown across portals
    Task<List<AnnouncementDto>> ListAllAsync(long tenantId, CancellationToken ct = default);
    Task<AnnouncementDto> UpdateAsync(long tenantId, long id, UpdateAnnouncementRequest req, CancellationToken ct = default);
}

public class AnnouncementService : IAnnouncementService
{
    private readonly LearnCloudDbContext _db;
    public AnnouncementService(LearnCloudDbContext db) => _db = db;

    public async Task<AnnouncementDto> CreateAsync(long tenantId, long userId, CreateAnnouncementRequest req, CancellationToken ct = default)
    {
        var entity = new Announcement
        {
            TenantId = tenantId,
            Title = req.Title,
            Body = req.Body,
            AudienceType = req.AudienceType,
            AudienceFilterJson = req.AudienceFilterJson,
            ExpiryDate = req.ExpiryDate,
            Priority = req.Priority,
            ShowInTeacherPortal = req.ShowInTeacherPortal,
            ShowInParentPortal = req.ShowInParentPortal,
            ShowInStudentPortal = req.ShowInStudentPortal,
            ShowInAdminDashboard = req.ShowInAdminDashboard,
            CreatedByUserId = userId,
            PublishedAt = DateTime.UtcNow,
            Status = "active",
            CreatedBy = userId
        };
        _db.Set<Announcement>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<List<AnnouncementDto>> ListActiveAsync(long tenantId, string portal, CancellationToken ct = default)
    {
        // Shown across portals until expiry date
        var now = DateTime.UtcNow;
        var query = _db.Set<Announcement>().Where(a => a.TenantId == tenantId && !a.IsDeleted && a.Status == "active" && (a.ExpiryDate == null || a.ExpiryDate > now));

        query = portal.ToLower() switch
        {
            "teacher" => query.Where(a => a.ShowInTeacherPortal),
            "parent" => query.Where(a => a.ShowInParentPortal),
            "student" => query.Where(a => a.ShowInStudentPortal),
            "admin" => query.Where(a => a.ShowInAdminDashboard),
            _ => query
        };

        var list = await query.OrderByDescending(a => a.Priority == "urgent").ThenByDescending(a => a.CreatedAt).Take(20).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<List<AnnouncementDto>> ListAllAsync(long tenantId, CancellationToken ct = default)
    {
        var list = await _db.Set<Announcement>().Where(a => a.TenantId == tenantId && !a.IsDeleted).OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<AnnouncementDto> UpdateAsync(long tenantId, long id, UpdateAnnouncementRequest req, CancellationToken ct = default)
    {
        var entity = await _db.Set<Announcement>().FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, ct) ?? throw new InvalidOperationException("Announcement not found");
        if (req.Title != null) entity.Title = req.Title;
        if (req.Body != null) entity.Body = req.Body;
        if (req.ExpiryDate.HasValue) entity.ExpiryDate = req.ExpiryDate;
        if (req.Status != null) entity.Status = req.Status;
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static AnnouncementDto Map(Announcement a) => new(a.Id, a.Title, a.Body, a.AudienceType, a.AudienceFilterJson, a.ExpiryDate, a.Status, a.Priority, a.ShowInTeacherPortal, a.ShowInParentPortal, a.ShowInStudentPortal, a.ShowInAdminDashboard, a.CreatedAt, a.CreatedByUserId);
}

public interface IAudienceSegmentService
{
    Task<AudienceSegmentDto> CreateAsync(long tenantId, long userId, CreateAudienceSegmentRequest req, CancellationToken ct = default);
    Task<List<AudienceSegmentDto>> ListAsync(long tenantId, CancellationToken ct = default);
    Task<PreviewSegmentResponse> PreviewAsync(long tenantId, PreviewSegmentRequest req, CancellationToken ct = default);
}

public class AudienceSegmentService : IAudienceSegmentService
{
    private readonly LearnCloudDbContext _db;
    private readonly Messaging.Services.AudienceResolver _audienceResolver; // reuse existing resolver from messaging module

    public AudienceSegmentService(LearnCloudDbContext db, Messaging.Services.AudienceResolver audienceResolver)
    {
        _db = db;
        _audienceResolver = audienceResolver;
    }

    public async Task<AudienceSegmentDto> CreateAsync(long tenantId, long userId, CreateAudienceSegmentRequest req, CancellationToken ct = default)
    {
        var entity = new AudienceSegment
        {
            TenantId = tenantId,
            Name = req.Name,
            Description = req.Description,
            AudienceType = req.AudienceType,
            FilterJson = req.FilterJson,
            IsDynamic = req.IsDynamic,
            CreatedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<AudienceSegment>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return new AudienceSegmentDto(entity.Id, entity.Name, entity.Description, entity.AudienceType, entity.FilterJson, entity.IsDynamic, entity.CreatedByUserId, entity.IsSystem, entity.CreatedAt);
    }

    public async Task<List<AudienceSegmentDto>> ListAsync(long tenantId, CancellationToken ct = default)
    {
        var list = await _db.Set<AudienceSegment>().Where(s => s.TenantId == tenantId && !s.IsDeleted).OrderBy(s => s.Name).ToListAsync(ct);
        return list.Select(s => new AudienceSegmentDto(s.Id, s.Name, s.Description, s.AudienceType, s.FilterJson, s.IsDynamic, s.CreatedByUserId, s.IsSystem, s.CreatedAt)).ToList();
    }

    public async Task<PreviewSegmentResponse> PreviewAsync(long tenantId, PreviewSegmentRequest req, CancellationToken ct = default)
    {
        // For dynamic segments, re-evaluate at send time
        AudienceRequest audienceReq;
        if (req.SegmentId.HasValue)
        {
            var segment = await _db.Set<AudienceSegment>().FirstOrDefaultAsync(s => s.Id == req.SegmentId.Value && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Segment not found");
            var filter = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(segment.FilterJson) ?? new Dictionary<string, object>();
            audienceReq = new AudienceRequest(segment.AudienceType, filter.TryGetValue("gradeId", out var g) ? Convert.ToInt64(g) : null, filter.TryGetValue("streamId", out var s) ? Convert.ToInt64(s) : null, null, filter.TryGetValue("arrearsThreshold", out var a) ? Convert.ToDecimal(a) : null, null, null, null);
        }
        else
        {
            audienceReq = new AudienceRequest(req.AudienceType, null, null, null, null, null, null, null);
        }

        var recipients = await _audienceResolver.ResolveAsync(tenantId, audienceReq, ct);
        var sample = recipients.Take(5).Select(r => new SampleRecipientDto(r.GuardianId, r.GuardianName, r.Contact, r.StudentId, r.StudentName)).ToList();

        return new PreviewSegmentResponse(recipients.Count, recipients.Count, sample);
    }
}

// Template library with categories
public interface ITemplateCategoryService
{
    Task<List<TemplateCategoryDto>> ListCategoriesAsync(long tenantId, CancellationToken ct = default);
    Task<TemplateCategoryDto> CreateCategoryAsync(long tenantId, CreateTemplateCategoryRequest req, CancellationToken ct = default);
    Task<List<TemplateWithCategoryDto>> ListTemplatesWithCategoryAsync(long tenantId, string? categoryCode, CancellationToken ct = default);
}

public class TemplateCategoryService : ITemplateCategoryService
{
    private readonly LearnCloudDbContext _db;
    public TemplateCategoryService(LearnCloudDbContext db) => _db = db;

    public async Task<List<TemplateCategoryDto>> ListCategoriesAsync(long tenantId, CancellationToken ct = default)
    {
        var cats = await _db.Set<TemplateCategory>().Where(c => c.TenantId == tenantId && !c.IsDeleted).ToListAsync(ct);
        if (!cats.Any())
        {
            // Seed default categories: fees, attendance, academic, general, discipline
            var defaults = new List<TemplateCategory>
            {
                new() { TenantId = tenantId, Name = "Fees", Code = "fees", Description = "Fee reminders, invoices, receipts", Color = "#B7791F" },
                new() { TenantId = tenantId, Name = "Attendance", Code = "attendance", Description = "Absence, late, chronic absence", Color = "#C62828" },
                new() { TenantId = tenantId, Name = "Academic", Code = "academic", Description = "Report cards, assessments, homework", Color = "#5A94C1" },
                new() { TenantId = tenantId, Name = "General", Code = "general", Description = "General notices", Color = "#0F153A" },
                new() { TenantId = tenantId, Name = "Discipline", Code = "discipline", Description = "Discipline notices", Color = "#3C2C59" },
            };
            _db.Set<TemplateCategory>().AddRange(defaults);
            await _db.SaveChangesAsync(ct);
            cats = defaults;
        }
        return cats.Select(c => new TemplateCategoryDto(c.Id, c.Name, c.Code, c.Description, c.Color)).ToList();
    }

    public async Task<TemplateCategoryDto> CreateCategoryAsync(long tenantId, CreateTemplateCategoryRequest req, CancellationToken ct = default)
    {
        var entity = new TemplateCategory { TenantId = tenantId, Name = req.Name, Code = req.Code, Description = req.Description, Color = req.Color };
        _db.Set<TemplateCategory>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return new TemplateCategoryDto(entity.Id, entity.Name, entity.Code, entity.Description, entity.Color);
    }

    public async Task<List<TemplateWithCategoryDto>> ListTemplatesWithCategoryAsync(long tenantId, string? categoryCode, CancellationToken ct = default)
    {
        var query = _db.Set<Messaging.Entities.MessageTemplate>().Where(t => t.TenantId == tenantId && !t.IsDeleted);
        if (!string.IsNullOrEmpty(categoryCode))
        {
            var cat = await _db.Set<TemplateCategory>().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == categoryCode && !c.IsDeleted, ct);
            if (cat != null)
            {
                var templateIds = await _db.Set<CategorizedTemplate>().Where(ctt => ctt.CategoryId == cat.Id && ctt.TenantId == tenantId).Select(ctt => ctt.TemplateId).ToListAsync(ct);
                query = query.Where(t => templateIds.Contains(t.Id));
            }
        }

        var templates = await query.ToListAsync(ct);
        var result = new List<TemplateWithCategoryDto>();
        foreach (var t in templates)
        {
            var catLink = await _db.Set<CategorizedTemplate>().FirstOrDefaultAsync(ctt => ctt.TemplateId == t.Id && ctt.TenantId == tenantId, ct);
            TemplateCategory? cat = null;
            if (catLink != null) cat = await _db.Set<TemplateCategory>().FirstOrDefaultAsync(c => c.Id == catLink.CategoryId, ct);

            var fields = !string.IsNullOrEmpty(t.MergeFieldsJson) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(t.MergeFieldsJson) ?? new List<string>() : new List<string>();
            result.Add(new TemplateWithCategoryDto(t.Id, t.Name, t.Code, t.Channel.ToString().ToLower(), t.Subject, t.Body, t.IsSystem, cat?.Name, cat?.Code, fields));
        }
        return result;
    }
}

// Two-way SMS handling if provider supports it
public interface ITwoWaySmsService
{
    Task<InboundSmsDto> HandleInboundAsync(long tenantId, WebhookInboundSmsRequest req, CancellationToken ct = default);
    Task<List<InboundSmsDto>> ListInboundAsync(long tenantId, CancellationToken ct = default);
    Task<InboundSmsDto> ReplyAsync(long tenantId, long userId, ReplyInboundSmsRequest req, CancellationToken ct = default);
}

public class TwoWaySmsService : ITwoWaySmsService
{
    private readonly LearnCloudDbContext _db;
    private readonly Messaging.Services.Providers.IMessagingProviderFactory _providerFactory;

    public TwoWaySmsService(LearnCloudDbContext db, Messaging.Services.Providers.IMessagingProviderFactory providerFactory)
    {
        _db = db;
        _providerFactory = providerFactory;
    }

    public async Task<InboundSmsDto> HandleInboundAsync(long tenantId, WebhookInboundSmsRequest req, CancellationToken ct = default)
    {
        // Match guardian by phone
        var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Phone == req.From && !g.IsDeleted, ct);

        var entity = new InboundSms
        {
            TenantId = tenantId,
            FromNumber = req.From,
            ToNumber = req.To,
            Body = req.Text,
            Provider = req.Provider ?? "Unknown",
            ProviderReference = req.ProviderReference,
            ReceivedAt = DateTime.UtcNow,
            MatchedGuardianId = guardian?.Id,
            Status = guardian != null ? "matched" : "unmatched"
        };
        _db.Set<InboundSms>().Add(entity);
        await _db.SaveChangesAsync(ct);

        // Also create communication log for searchable by learner
        if (guardian != null)
        {
            var studentLinks = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.GuardianId == guardian.Id && !l.IsDeleted).ToListAsync(ct);
            foreach (var link in studentLinks)
            {
                _db.Set<CommunicationLog>().Add(new CommunicationLog
                {
                    TenantId = tenantId,
                    InboundSmsId = entity.Id,
                    StudentId = link.StudentId,
                    GuardianId = guardian.Id,
                    Channel = "sms",
                    Direction = "inbound",
                    RecipientAddress = req.From,
                    MessageBody = req.Text,
                    Status = "received",
                    Provider = req.Provider,
                    SentAt = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        return new InboundSmsDto(entity.Id, entity.FromNumber, entity.ToNumber, entity.Body, entity.Provider, entity.ProviderReference, entity.ReceivedAt, entity.MatchedGuardianId, entity.MatchedStudentId, entity.Status, entity.ReplyBody, entity.RepliedAt);
    }

    public async Task<List<InboundSmsDto>> ListInboundAsync(long tenantId, CancellationToken ct = default)
    {
        var list = await _db.Set<InboundSms>().Where(i => i.TenantId == tenantId && !i.IsDeleted).OrderByDescending(i => i.ReceivedAt).Take(100).ToListAsync(ct);
        return list.Select(i => new InboundSmsDto(i.Id, i.FromNumber, i.ToNumber, i.Body, i.Provider, i.ProviderReference, i.ReceivedAt, i.MatchedGuardianId, i.MatchedStudentId, i.Status, i.ReplyBody, i.RepliedAt)).ToList();
    }

    public async Task<InboundSmsDto> ReplyAsync(long tenantId, long userId, ReplyInboundSmsRequest req, CancellationToken ct = default)
    {
        var inbound = await _db.Set<InboundSms>().FirstOrDefaultAsync(i => i.Id == req.InboundSmsId && i.TenantId == tenantId && !i.IsDeleted, ct) ?? throw new InvalidOperationException("Inbound SMS not found");

        var smsProvider = await _providerFactory.GetSmsProviderAsync(tenantId, ct);
        var smsMsg = new Messaging.Services.Providers.SmsMessage { To = inbound.FromNumber, Body = req.ReplyBody };
        var result = await smsProvider.SendAsync(smsMsg, ct);

        inbound.ReplyBody = req.ReplyBody;
        inbound.RepliedAt = DateTime.UtcNow;
        inbound.Status = result.Success ? "replied" : "reply_failed";

        await _db.SaveChangesAsync(ct);

        return new InboundSmsDto(inbound.Id, inbound.FromNumber, inbound.ToNumber, inbound.Body, inbound.Provider, inbound.ProviderReference, inbound.ReceivedAt, inbound.MatchedGuardianId, inbound.MatchedStudentId, inbound.Status, inbound.ReplyBody, inbound.RepliedAt);
    }
}

// Event-triggered rule engine
public interface ICommunicationRuleEngine
{
    Task<List<RuleDto>> ListRulesAsync(long tenantId, CancellationToken ct = default);
    Task<RuleDto> CreateRuleAsync(long tenantId, long userId, CreateRuleRequest req, CancellationToken ct = default);
    Task CheckAndTriggerAsync(long tenantId, string eventType, TriggerRuleRequest trigger, CancellationToken ct = default);
    Task ProcessScheduledRulesAsync(CancellationToken ct = default); // runs via background job
}

public class CommunicationRuleEngine : ICommunicationRuleEngine
{
    private readonly LearnCloudDbContext _db;
    private readonly Messaging.Services.AudienceResolver _audienceResolver;
    private readonly ILogger<CommunicationRuleEngine> _logger;

    public CommunicationRuleEngine(LearnCloudDbContext db, Messaging.Services.AudienceResolver audienceResolver, ILogger<CommunicationRuleEngine> logger)
    {
        _db = db;
        _audienceResolver = audienceResolver;
        _logger = logger;
    }

    public async Task<List<RuleDto>> ListRulesAsync(long tenantId, CancellationToken ct = default)
    {
        var rules = await _db.Set<CommunicationRule>().Where(r => r.TenantId == tenantId && !r.IsDeleted).ToListAsync(ct);
        return rules.Select(r => new RuleDto(r.Id, r.Name, r.Code, r.EventType, r.Description, r.IsActive, r.ConfigJson, r.TemplateId, r.Channel, r.AudienceType, r.RespectOptOut, r.LastTriggeredAt, r.TriggerCount, r.CreatedAt)).ToList();
    }

    public async Task<RuleDto> CreateRuleAsync(long tenantId, long userId, CreateRuleRequest req, CancellationToken ct = default)
    {
        var entity = new CommunicationRule
        {
            TenantId = tenantId,
            Name = req.Name,
            Code = req.Code,
            EventType = req.EventType,
            Description = req.Description,
            IsActive = req.IsActive,
            ConfigJson = req.ConfigJson,
            TemplateId = req.TemplateId,
            Channel = req.Channel,
            AudienceType = req.AudienceType,
            RespectOptOut = req.RespectOptOut,
            CreatedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<CommunicationRule>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return new RuleDto(entity.Id, entity.Name, entity.Code, entity.EventType, entity.Description, entity.IsActive, entity.ConfigJson, entity.TemplateId, entity.Channel, entity.AudienceType, entity.RespectOptOut, entity.LastTriggeredAt, entity.TriggerCount, entity.CreatedAt);
    }

    public async Task CheckAndTriggerAsync(long tenantId, string eventType, TriggerRuleRequest trigger, CancellationToken ct = default)
    {
        var rules = await _db.Set<CommunicationRule>().Where(r => r.TenantId == tenantId && r.EventType == eventType && r.IsActive && !r.IsDeleted).ToListAsync(ct);

        foreach (var rule in rules)
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(rule.ConfigJson) ?? new Dictionary<string, object>();

            bool shouldTrigger = false;
            string reason = "";

            switch (eventType)
            {
                case "absence_n_days":
                    // Config: {"n":3}
                    var n = config.TryGetValue("n", out var nObj) ? Convert.ToInt32(nObj) : 3;
                    // Check if student absent for N consecutive days
                    if (trigger.StudentId.HasValue)
                    {
                        var consecutive = await CountConsecutiveAbsenceAsync(tenantId, trigger.StudentId.Value, ct);
                        if (consecutive >= n)
                        {
                            shouldTrigger = true;
                            reason = $"Absent {consecutive} consecutive days >= threshold {n}";
                        }
                    }
                    break;
                case "arrears_over_threshold":
                    // Config: {"threshold":100,"currency":"USD"}
                    var threshold = config.TryGetValue("threshold", out var thrObj) ? Convert.ToDecimal(thrObj) : 100m;
                    if (trigger.Amount.HasValue && trigger.Amount.Value >= threshold)
                    {
                        shouldTrigger = true;
                        reason = $"Arrears {trigger.Amount} >= threshold {threshold}";
                    }
                    break;
                case "report_card_published":
                    shouldTrigger = true;
                    reason = $"Report card published for student {trigger.StudentId}";
                    break;
                case "invoice_due_7_days":
                    // Config: {"daysBeforeDue":7}
                    var daysBefore = config.TryGetValue("daysBeforeDue", out var dObj) ? Convert.ToInt32(dObj) : 7;
                    if (trigger.Date.HasValue)
                    {
                        var daysUntilDue = (trigger.Date.Value - DateTime.UtcNow.Date).Days;
                        if (daysUntilDue == daysBefore)
                        {
                            shouldTrigger = true;
                            reason = $"Invoice due in {daysBefore} days on {trigger.Date:yyyy-MM-dd}";
                        }
                    }
                    break;
            }

            if (shouldTrigger)
            {
                _logger.LogInformation("Rule {RuleCode} triggered for tenant {TenantId}: {Reason}", rule.Code, tenantId, reason);
                // Create message batch from rule template
                await CreateBatchFromRuleAsync(rule, trigger, ct);
                rule.LastTriggeredAt = DateTime.UtcNow;
                rule.TriggerCount++;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task ProcessScheduledRulesAsync(CancellationToken ct = default)
    {
        // Runs via background job every hour, checks for events that need triggering
        var activeRules = await _db.Set<CommunicationRule>().Where(r => r.TenantId == tenantId && r.IsActive && !r.IsDeleted).ToListAsync(ct); // SECURITY C5 FIX: Added TenantId filter

        foreach (var rule in activeRules.GroupBy(r => r.TenantId))
        {
            var tenantId = rule.Key;
            foreach (var r in rule)
            {
                try
                {
                    // For each rule type, scan for matching conditions
                    if (r.EventType == "absence_n_days")
                    {
                        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(r.ConfigJson);
                        var n = config != null && config.TryGetValue("n", out var nObj) ? Convert.ToInt32(nObj) : 3;
                        // Find students with N consecutive absence
                        var students = await _db.Set<Student>().Where(s => s.TenantId == tenantId && !s.IsDeleted).Take(100).ToListAsync(ct); // limit for demo
                        foreach (var student in students)
                        {
                            var consecutive = await CountConsecutiveAbsenceAsync(tenantId, student.Id, ct);
                            if (consecutive >= n)
                            {
                                await CheckAndTriggerAsync(tenantId, r.EventType, new TriggerRuleRequest(student.Id, null, null, null, null, $"{{\"consecutive\":{consecutive}}}"), ct);
                            }
                        }
                    }
                    // Other event types similar...
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process scheduled rule {RuleId} tenant {TenantId}", r.Id, tenantId);
                }
            }
        }
    }

    private async Task<int> CountConsecutiveAbsenceAsync(long tenantId, long studentId, CancellationToken ct)
    {
        var records = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == studentId && !a.IsDeleted).OrderByDescending(a => a.AttendanceDate).Take(10).ToListAsync(ct);
        int consecutive = 0;
        var date = DateTime.UtcNow.Date;
        foreach (var rec in records.OrderByDescending(r => r.AttendanceDate))
        {
            if (rec.Status == AttendanceStatus.Absent || rec.Status == AttendanceStatus.Sick)
            {
                // Check if consecutive day? Simplified: count consecutive absent records regardless of weekend
                consecutive++;
                date = rec.AttendanceDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }
        return consecutive;
    }

    private async Task CreateBatchFromRuleAsync(CommunicationRule rule, TriggerRuleRequest trigger, CancellationToken ct)
    {
        // Create MessageBatch from rule template
        var template = rule.TemplateId.HasValue ? await _db.Set<Messaging.Entities.MessageTemplate>().FirstOrDefaultAsync(t => t.Id == rule.TemplateId.Value && !t.IsDeleted, ct) : null;

        var batch = new Messaging.Entities.MessageBatch
        {
            TenantId = rule.TenantId,
            BatchNumber = $"RULE-{rule.Code}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Title = $"{rule.Name} - Auto triggered",
            TemplateId = rule.TemplateId,
            Channel = rule.Channel.ToLower() == "sms" ? Messaging.Entities.MessageChannel.Sms : Messaging.Entities.MessageChannel.Email,
            AudienceType = (Messaging.Entities.AudienceType)Enum.Parse(typeof(Messaging.Entities.AudienceType), rule.AudienceType, true),
            AudienceFilterJson = $"{{\"studentId\":{trigger.StudentId},\"reason\":\"{rule.Code}\"}}",
            Body = template?.Body ?? $"Auto message for {rule.EventType}",
            Subject = template?.Subject,
            TotalRecipients = 1, // will be updated after audience resolution
            EstimatedCost = 0.05m,
            Currency = "USD",
            Status = Messaging.Entities.MessageStatus.Queued,
            CreatedByUserId = rule.CreatedByUserId,
            CreatedBy = rule.CreatedByUserId,
            QueuedAt = DateTime.UtcNow
        };

        _db.Set<Messaging.Entities.MessageBatch>().Add(batch);
        await _db.SaveChangesAsync(ct);

        // For demo, create one delivery log for the student
        if (trigger.StudentId.HasValue)
        {
            var links = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == rule.TenantId && l.StudentId == trigger.StudentId.Value && !l.IsDeleted).ToListAsync(ct);
            foreach (var link in links)
            {
                var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == link.GuardianId, ct);
                if (guardian == null) continue;

                // Respect opt-out
                if (rule.RespectOptOut)
                {
                    var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == rule.TenantId && p.GuardianId == guardian.Id && !p.IsDeleted, ct);
                    if (pref != null && ((batch.Channel == Messaging.Entities.MessageChannel.Sms && (pref.SmsOptOut || !pref.SmsOptIn)) || (batch.Channel == Messaging.Entities.MessageChannel.Email && (pref.EmailOptOut || !pref.EmailOptIn))))
                    {
                        continue; // skip opted out
                    }
                }

                _db.Set<Messaging.Entities.MessageDeliveryLog>().Add(new Messaging.Entities.MessageDeliveryLog
                {
                    TenantId = rule.TenantId,
                    BatchId = batch.Id,
                    GuardianId = guardian.Id,
                    StudentId = trigger.StudentId,
                    RecipientName = $"{guardian.FirstName} {guardian.LastName}",
                    RecipientAddress = batch.Channel == Messaging.Entities.MessageChannel.Sms ? guardian.Phone : guardian.Email ?? guardian.Phone,
                    Channel = batch.Channel,
                    Status = Messaging.Entities.MessageStatus.Queued,
                    RenderedBody = batch.Body,
                    Currency = "USD",
                    Cost = 0.05m
                });
            }
            await _db.SaveChangesAsync(ct);
        }
    }
}

// Stub classes for compilation

// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Final cleanup - single source of truth
