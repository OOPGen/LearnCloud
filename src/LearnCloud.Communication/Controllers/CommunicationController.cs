using LearnCloud.Communication.DTOs;
using LearnCloud.Communication.Entities;
using LearnCloud.Communication.Services;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Communication.Controllers;

[ApiController]
[Route("api/communication")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class CommunicationController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAnnouncementService _announcementService;
    private readonly IAudienceSegmentService _segmentService;
    private readonly ITemplateCategoryService _categoryService;
    private readonly ITwoWaySmsService _twoWayService;
    private readonly ICommunicationRuleEngine _ruleEngine;

    public CommunicationController(LearnCloudDbContext db, ITenantContext tenantContext, IAnnouncementService announcementService, IAudienceSegmentService segmentService, ITemplateCategoryService categoryService, ITwoWaySmsService twoWayService, ICommunicationRuleEngine ruleEngine)
    {
        _db = db;
        _tenantContext = tenantContext;
        _announcementService = announcementService;
        _segmentService = segmentService;
        _categoryService = categoryService;
        _twoWayService = twoWayService;
        _ruleEngine = ruleEngine;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Announcements with audience and expiry date shown across portals
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("announcements")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest req, CancellationToken ct)
    {
        var ann = await _announcementService.CreateAsync(TenantId, UserId, req, ct);
        return Ok(ann);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("announcements")]
    public async Task<IActionResult> ListAnnouncements([FromQuery] string? portal, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(portal))
        {
            var all = await _announcementService.ListAllAsync(TenantId, ct);
            return Ok(all);
        }
        else
        {
            var active = await _announcementService.ListActiveAsync(TenantId, portal, ct);
            return Ok(active);
        }
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("announcements/{id:long}")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> UpdateAnnouncement(long id, [FromBody] UpdateAnnouncementRequest req, CancellationToken ct)
    {
        var ann = await _announcementService.UpdateAsync(TenantId, id, req, ct);
        return Ok(ann);
    }

    // Scheduled sending
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("scheduled")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,BURSAR")]
    public async Task<IActionResult> CreateScheduled([FromBody] CreateScheduledMessageRequest req, CancellationToken ct)
    {
        var entity = new ScheduledMessage
        {
            TenantId = TenantId,
            Title = req.Title,
            TemplateId = req.TemplateId,
            Channel = req.Channel,
            AudienceType = req.AudienceType,
            AudienceFilterJson = req.AudienceFilterJson,
            Body = req.Body,
            Subject = req.Subject,
            ScheduledSendAt = req.ScheduledSendAt,
            Status = "scheduled",
            CreatedByUserId = UserId,
            CreatedBy = UserId
        };
        _db.Set<ScheduledMessage>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new ScheduledMessageDto(entity.Id, entity.Title, entity.TemplateId, entity.Channel, entity.AudienceType, entity.Body, entity.Subject, entity.ScheduledSendAt, entity.Status, entity.CreatedByUserId, entity.CreatedAt));
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("scheduled")]
    public async Task<IActionResult> ListScheduled(CancellationToken ct)
    {
        var list = await _db.Set<ScheduledMessage>().Where(s => s.TenantId == TenantId && !s.IsDeleted).OrderBy(s => s.ScheduledSendAt).ToListAsync(ct);
        return Ok(list.Select(s => new ScheduledMessageDto(s.Id, s.Title, s.TemplateId, s.Channel, s.AudienceType, s.Body, s.Subject, s.ScheduledSendAt, s.Status, s.CreatedByUserId, s.CreatedAt)));
    }

    // Saved audience segments
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("segments")]
    public async Task<IActionResult> CreateSegment([FromBody] CreateAudienceSegmentRequest req, CancellationToken ct)
    {
        var seg = await _segmentService.CreateAsync(TenantId, UserId, req, ct);
        return Ok(seg);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("segments")]
    public async Task<IActionResult> ListSegments(CancellationToken ct)
    {
        var list = await _segmentService.ListAsync(TenantId, ct);
        return Ok(list);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("segments/preview")]
    public async Task<IActionResult> PreviewSegment([FromBody] PreviewSegmentRequest req, CancellationToken ct)
    {
        var preview = await _segmentService.PreviewAsync(TenantId, req, ct);
        return Ok(preview);
    }

    // Template library with categories
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("template-categories")]
    public async Task<IActionResult> ListCategories(CancellationToken ct)
    {
        var cats = await _categoryService.ListCategoriesAsync(TenantId, ct);
        return Ok(cats);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("template-categories")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateTemplateCategoryRequest req, CancellationToken ct)
    {
        var cat = await _categoryService.CreateCategoryAsync(TenantId, req, ct);
        return Ok(cat);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("templates/with-category")]
    public async Task<IActionResult> ListTemplatesWithCategory([FromQuery] string? categoryCode, CancellationToken ct)
    {
        var list = await _categoryService.ListTemplatesWithCategoryAsync(TenantId, categoryCode, ct);
        return Ok(list);
    }

    // Two-way SMS handling if provider supports it
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("inbound-sms/webhook")]
    [AllowAnonymous] // provider webhook
    public async Task<IActionResult> InboundSmsWebhook([FromBody] WebhookInboundSmsRequest req, CancellationToken ct)
    {
        // Resolve tenant by ToNumber? For V1, use X-Tenant-Id header or domain mapping
        // For demo, use first tenant or tenant from header
        var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        long tenantId = TenantId;
        if (!string.IsNullOrEmpty(tenantIdHeader) && long.TryParse(tenantIdHeader, out var tid)) tenantId = tid;
        else if (_tenantContext.TenantId == null) tenantId = 1; // fallback for webhook test

        var result = await _twoWayService.HandleInboundAsync(tenantId, req, ct);
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("inbound-sms")]
    public async Task<IActionResult> ListInboundSms(CancellationToken ct)
    {
        var list = await _twoWayService.ListInboundAsync(TenantId, ct);
        return Ok(list);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("inbound-sms/{id:long}/reply")]
    public async Task<IActionResult> ReplyInboundSms(long id, [FromBody] ReplyInboundSmsRequest req, CancellationToken ct)
    {
        var result = await _twoWayService.ReplyAsync(TenantId, UserId, req with { InboundSmsId = id }, ct);
        return Ok(result);
    }

    // Event-triggered rule engine with per-tenant configuration and opt-out
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("rules")]
    public async Task<IActionResult> ListRules(CancellationToken ct)
    {
        var rules = await _ruleEngine.ListRulesAsync(TenantId, ct);
        return Ok(rules);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("rules")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest req, CancellationToken ct)
    {
        var rule = await _ruleEngine.CreateRuleAsync(TenantId, UserId, req, ct);
        return Ok(rule);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("rules/{id:long}/trigger")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> TriggerRule(long id, [FromBody] TriggerRuleRequest trigger, CancellationToken ct)
    {
        var rule = await _db.Set<CommunicationRule>().FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted, ct);
        if (rule == null) return NotFound();

        await _ruleEngine.CheckAndTriggerAsync(TenantId, rule.EventType, trigger, ct);
        return Ok(new { message = $"Rule {rule.Code} triggered", ruleId = id });
    }

    // Delivery analytics by campaign
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("analytics/campaigns")]
    public async Task<IActionResult> GetCampaignAnalytics([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var query = _db.Set<Messaging.Entities.MessageBatch>().Where(b => b.TenantId == TenantId && !b.IsDeleted);
        if (from.HasValue) query = query.Where(b => b.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(b => b.CreatedAt <= to.Value);

        var batches = await query.OrderByDescending(b => b.CreatedAt).Take(50).ToListAsync(ct);

        var analytics = new List<CampaignAnalytics>();
        foreach (var batch in batches)
        {
            var logs = await _db.Set<Messaging.Entities.MessageDeliveryLog>().Where(l => l.BatchId == batch.Id && l.TenantId == TenantId && !l.IsDeleted).ToListAsync(ct);
            var sent = logs.Count(l => l.Status == Messaging.Entities.MessageStatus.Sent || l.Status == Messaging.Entities.MessageStatus.Delivered);
            var delivered = logs.Count(l => l.Status == Messaging.Entities.MessageStatus.Delivered);
            var failed = logs.Count(l => l.Status == Messaging.Entities.MessageStatus.Failed);
            var read = logs.Count(l => l.Status == Messaging.Entities.MessageStatus.Delivered); // simplified read = delivered
            var totalCost = logs.Sum(l => l.Cost);
            var deliveryRate = batch.TotalRecipients > 0 ? (double)delivered / batch.TotalRecipients * 100 : 0;
            var readRate = delivered > 0 ? (double)read / delivered * 100 : 0;

            analytics.Add(new CampaignAnalytics
            {
                BatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                Title = batch.Title,
                Channel = batch.Channel.ToString(),
                TotalRecipients = batch.TotalRecipients,
                Sent = sent,
                Delivered = delivered,
                Failed = failed,
                Read = read,
                Replied = 0,
                TotalCost = totalCost,
                Currency = batch.Currency,
                DeliveryRate = Math.Round(deliveryRate, 2),
                ReadRate = Math.Round(readRate, 2),
                ReplyRate = 0
            });
        }

        return Ok(analytics.Select(a => new CampaignAnalyticsDto(a.BatchId, a.BatchNumber, a.Title, a.Channel, a.TotalRecipients, a.Sent, a.Delivered, a.Failed, a.Read, a.Replied, a.TotalCost, a.Currency, a.DeliveryRate, a.ReadRate, a.ReplyRate, new List<DailyStatDto>())));
    }

    // Per-tenant communication log searchable by learner
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("logs")]
    public async Task<IActionResult> SearchLogs([FromQuery] CommunicationLogSearchRequest req, CancellationToken ct)
    {
        var query = _db.Set<CommunicationLog>().Where(l => l.TenantId == TenantId && !l.IsDeleted);

        if (req.StudentId.HasValue) query = query.Where(l => l.StudentId == req.StudentId.Value);
        if (req.GuardianId.HasValue) query = query.Where(l => l.GuardianId == req.GuardianId.Value);
        if (!string.IsNullOrEmpty(req.Channel)) query = query.Where(l => l.Channel == req.Channel);
        if (!string.IsNullOrEmpty(req.Status)) query = query.Where(l => l.Status == req.Status);
        if (req.FromDate.HasValue) query = query.Where(l => l.SentAt >= req.FromDate.Value);
        if (req.ToDate.HasValue) query = query.Where(l => l.SentAt <= req.ToDate.Value);
        if (!string.IsNullOrEmpty(req.SearchText)) query = query.Where(l => l.MessageBody.Contains(req.SearchText) || l.RecipientAddress.Contains(req.SearchText));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.SentAt).Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).ToListAsync(ct);

        var dtos = new List<CommunicationLogDto>();
        foreach (var log in items)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == log.StudentId, ct);
            var guardian = log.GuardianId.HasValue ? await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == log.GuardianId.Value, ct) : null;
            dtos.Add(new CommunicationLogDto(log.Id, log.BatchId, log.AnnouncementId, log.RuleId, log.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", log.GuardianId, guardian != null ? $"{guardian.FirstName} {guardian.LastName}" : "", log.Channel, log.Direction, log.RecipientAddress, log.MessageBody, log.Subject, log.Status, log.Provider, log.Cost, log.Currency, log.SentAt, log.DeliveredAt, log.ReadAt, log.FailureReason));
        }

        return Ok(new PagedCommunicationLogDto(dtos, total, req.Page, req.PageSize));
    }
}

// Stub entities for compilation

// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Final cleanup - single source of truth