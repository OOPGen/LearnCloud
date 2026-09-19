using LearnCloud.Messaging.DTOs;
using LearnCloud.Messaging.Entities;
using LearnCloud.Messaging.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Messaging.Controllers;

[ApiController]
[Route("api/messaging")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class MessagingController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITemplateService _templateService;
    private readonly IServiceProvider _sp;

    public MessagingController(LearnCloudDbContext db, ITenantContext tenantContext, ITemplateService templateService, IServiceProvider sp)
    {
        _db = db; _tenantContext = tenantContext; _templateService = templateService; _sp = sp;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Templates with merge fields, three seeded templates
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
    {
        var list = await _templateService.ListAsync(TenantId, ct);
        return Ok(list);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("templates")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest req, CancellationToken ct)
    {
        var template = await _templateService.CreateAsync(TenantId, UserId, req, ct);
        return Ok(template);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("templates/seed")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> SeedTemplates(CancellationToken ct)
    {
        // Three seeded templates: fee reminder, absence notification, general notice
        var existing = await _db.Set<MessageTemplate>().Where(t => t.TenantId == TenantId && t.IsSystem && !t.IsDeleted).ToListAsync(ct);
        if (existing.Count >= 3) return Ok(new { message = "Already seeded", count = existing.Count });

        var templates = new List<MessageTemplate>
        {
            new MessageTemplate
            {
                TenantId = TenantId,
                Name = "Fee Reminder",
                Code = "fee_reminder",
                Channel = MessageChannel.Sms,
                Subject = "Fee Reminder - {{school_name}}",
                Body = "Dear {{guardian_name}}, learner {{learner_name}} ({{class}}) has outstanding balance {{currency}} {{amount_owed}} due {{due_date}}. Please settle at school. {{school_name}}",
                Description = "Sent to guardians of learners with arrears over X threshold",
                IsSystem = true,
                CreatedBy = UserId,
                MergeFieldsJson = "[\"guardian_name\",\"learner_name\",\"class\",\"amount_owed\",\"currency\",\"due_date\",\"school_name\"]"
            },
            new MessageTemplate
            {
                TenantId = TenantId,
                Name = "Absence Notification",
                Code = "absence_notification",
                Channel = MessageChannel.Sms,
                Subject = "Absence - {{learner_name}}",
                Body = "Dear {{guardian_name}}, {{learner_name}} ({{class}}) was absent today {{date}}. Reason: {{absence_reason}}. Please contact school. {{school_name}}",
                Description = "Sent to guardians of learners absent today",
                IsSystem = true,
                CreatedBy = UserId,
                MergeFieldsJson = "[\"guardian_name\",\"learner_name\",\"class\",\"date\",\"school_name\"]"
            },
            new MessageTemplate
            {
                TenantId = TenantId,
                Name = "General Notice",
                Code = "general_notice",
                Channel = MessageChannel.Email,
                Subject = "{{school_name}} - {{subject}}",
                Body = "Dear {{guardian_name}},\n\nThis is a notice for {{learner_name}} ({{class}}):\n\n{{message_body}}\n\nThank you,\n{{school_name}}\nDate: {{date}}",
                Description = "General notice to class, stream, year group or all guardians",
                IsSystem = true,
                CreatedBy = UserId,
                MergeFieldsJson = "[\"guardian_name\",\"learner_name\",\"class\",\"school_name\",\"date\",\"message_body\"]"
            }
        };

        // Email versions also
        templates.Add(new MessageTemplate
        {
            TenantId = TenantId,
            Name = "Fee Reminder - Email",
            Code = "fee_reminder_email",
            Channel = MessageChannel.Email,
            Subject = "Fee Reminder - {{learner_name}} - {{amount_owed}} {{currency}}",
            Body = "<p>Dear {{guardian_name}},</p><p>Learner <strong>{{learner_name}}</strong> ({{class}}) has outstanding balance <strong>{{currency}} {{amount_owed}}</strong> due {{due_date}}.</p><p>Please settle at accounts office.</p><p>Thank you,<br/>{{school_name}}</p>",
            Description = "Email fee reminder",
            IsSystem = true,
            CreatedBy = UserId
        });

        _db.Set<MessageTemplate>().AddRange(templates);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Seeded 4 templates (3 required + email variant)", count = templates.Count });
    }

    // Audience preview + cost estimate shown before confirming bulk send
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest req, CancellationToken ct)
    {
        var preview = await _templateService.PreviewAsync(TenantId, req, ct);
        return Ok(preview);
    }

    // Cost estimate endpoint
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("cost-estimate")]
    public async Task<IActionResult> CostEstimate([FromBody] PreviewRequest req, CancellationToken ct)
    {
        var preview = await _templateService.PreviewAsync(TenantId, req, ct);
        return Ok(preview.CostEstimate);
    }

    // Create batch - compose flow: audience selection, template choice, preview, recipient count, cost estimate and confirmation
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("batches")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,BURSAR,REGISTRAR")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateMessageBatchRequest req, CancellationToken ct)
    {
        // Resolve audience and cost estimate first
        var previewReq = new PreviewRequest(req.TemplateId, req.Channel, req.Audience, req.CustomBody ?? "", req.CustomSubject, null);
        var preview = await _templateService.PreviewAsync(TenantId, previewReq, ct);

        if (preview.CostEstimate.CapExceeded)
        {
            return BadRequest(new { message = "Hard cap exceeded", costEstimate = preview.CostEstimate, capWarning = preview.CostEstimate.CapWarningMessage });
        }

        // Create batch
        var batchNumber = $"MSG-{DateTime.UtcNow:yyyy}-{await _db.Set<MessageBatch>().CountAsync(b => b.TenantId == TenantId, ct) + 1:D5}";
        var batch = new MessageBatch
        {
            TenantId = TenantId,
            BatchNumber = batchNumber,
            Title = req.Title,
            TemplateId = req.TemplateId,
            Channel = req.Channel.ToLower() == "sms" ? MessageChannel.Sms : MessageChannel.Email,
            AudienceType = MapAudience(req.Audience.Type),
            AudienceFilterJson = System.Text.Json.JsonSerializer.Serialize(req.Audience),
            Body = req.CustomBody ?? "",
            Subject = req.CustomSubject,
            TotalRecipients = preview.TotalRecipients - preview.FilteredOptOut - preview.FilteredNoContact,
            EstimatedCost = preview.CostEstimate.TotalCost,
            Currency = preview.CostEstimate.Currency,
            Status = MessageStatus.Draft,
            CreatedByUserId = UserId,
            CreatedBy = UserId,
            CostEstimateJson = System.Text.Json.JsonSerializer.Serialize(preview.CostEstimate),
            IsPreviewed = true
        };

        // Load template body if templateId provided and custom body empty
        if (req.TemplateId.HasValue && string.IsNullOrWhiteSpace(req.CustomBody))
        {
            var template = await _db.Set<MessageTemplate>().FirstOrDefaultAsync(t => t.Id == req.TemplateId.Value && t.TenantId == TenantId, ct);
            if (template != null)
            {
                batch.Body = template.Body;
                batch.Subject = template.Subject;
            }
        }

        _db.Set<MessageBatch>().Add(batch);
        await _db.SaveChangesAsync(ct);

        // Create delivery logs for each recipient (with rendered body per recipient)
        var audienceResolver = new AudienceResolver(_db);
        var recipients = await audienceResolver.ResolveAsync(TenantId, req.Audience, ct);

        // Filter opt-out and no contact again for final logs
        var finalRecipients = new List<RecipientInfo>();
        int optedOut = 0, noContact = 0;
        foreach (var r in recipients)
        {
            var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == TenantId && p.GuardianId == r.GuardianId && !p.IsDeleted, ct);
            if (pref != null)
            {
                if (batch.Channel == MessageChannel.Sms && (pref.SmsOptOut || !pref.SmsOptIn)) { optedOut++; continue; }
                if (batch.Channel == MessageChannel.Email && (pref.EmailOptOut || !pref.EmailOptIn)) { optedOut++; continue; }
            }
            if (string.IsNullOrWhiteSpace(r.Contact))
            {
                noContact++;
                continue;
            }
            finalRecipients.Add(r);
        }

        foreach (var recipient in finalRecipients)
        {
            var mergeData = new Dictionary<string, string>
            {
                ["learner_name"] = recipient.StudentName ?? "",
                ["learner_first_name"] = recipient.StudentFirstName ?? "",
                ["class"] = recipient.ClassName ?? "",
                ["amount_owed"] = recipient.AmountOwed?.ToString("F2") ?? "0.00",
                ["currency"] = recipient.Currency ?? "USD",
                ["date"] = DateTime.UtcNow.ToString("dd/MM/yyyy"),
                ["school_name"] = recipient.SchoolName ?? "School",
                ["guardian_name"] = recipient.GuardianName ?? ""
            };

            var renderedBody = await _templateService.RenderAsync(batch.Body, mergeData);
            var renderedSubject = await _templateService.RenderAsync(batch.Subject ?? "", mergeData);

            var log = new MessageDeliveryLog
            {
                TenantId = TenantId,
                BatchId = batch.Id,
                GuardianId = recipient.GuardianId,
                StudentId = recipient.StudentId,
                RecipientName = recipient.GuardianName,
                RecipientAddress = recipient.Contact,
                Channel = batch.Channel,
                Status = MessageStatus.Queued,
                RenderedBody = renderedBody,
                RenderedSubject = renderedSubject,
                Currency = batch.Currency,
                CreatedBy = UserId
            };
            _db.Set<MessageDeliveryLog>().Add(log);
        }

        await _db.SaveChangesAsync(ct);

        // Return batch with cost estimate and warning
        return Ok(new
        {
            batch = new { batch.Id, batch.BatchNumber, batch.Title, batch.TotalRecipients, batch.EstimatedCost, batch.Currency, batch.Status },
            costEstimate = preview.CostEstimate,
            filteredOutOptOut = optedOut,
            filteredOutNoContact = noContact,
            capWarning = preview.CostEstimate.CapWarning ? preview.CostEstimate.CapWarningMessage : null
        });
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("batches/{id:long}/confirm")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,BURSAR")]
    public async Task<IActionResult> ConfirmBatch(long id, CancellationToken ct)
    {
        var batch = await _db.Set<MessageBatch>().FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId && !b.IsDeleted, ct);
        if (batch == null) return NotFound();
        if (batch.Status != MessageStatus.Draft) return BadRequest(new { message = $"Batch status {batch.Status} not Draft, cannot confirm" });

        // Hard per-tenant sending cap check
        var providerSettings = await _db.Set<MessagingProviderSettings>().FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Channel == batch.Channel && s.IsActive && !s.IsDeleted, ct);
        var cap = providerSettings?.DailyCap ?? 1000;
        var now = DateTime.UtcNow;
        var usage = await _db.Set<TenantMessagingUsage>().FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Year == now.Year && u.Month == now.Month && !u.IsDeleted, ct);
        var used = batch.Channel == MessageChannel.Sms ? (usage?.SmsCount ?? 0) : (usage?.EmailCount ?? 0);
        if (used + batch.TotalRecipients > cap)
        {
            return BadRequest(new { message = $"Hard cap exceeded: limit {cap}, used {used}, trying {batch.TotalRecipients}. Contact admin to increase cap.", cap, used, trying = batch.TotalRecipients });
        }

        batch.Status = MessageStatus.Queued;
        batch.QueuedAt = DateTime.UtcNow;
        // The send job is saved with the status change, and sent by the background job worker.
        _sp.GetRequiredService<Jobs.IMessageBatchQueue>().Enqueue(TenantId, batch.Id);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Batch queued for sending", batchId = batch.Id, status = batch.Status.ToString() });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("batches")]
    public async Task<IActionResult> ListBatches(CancellationToken ct)
    {
        var batches = await _db.Set<MessageBatch>().Where(b => b.TenantId == TenantId && !b.IsDeleted).OrderByDescending(b => b.CreatedAt).Take(50).ToListAsync(ct);
        var dtos = batches.Select(b => new MessageBatchDto(b.Id, b.BatchNumber, b.Title, b.TemplateId, b.Channel.ToString(), b.AudienceType.ToString(), b.Body, b.Subject, b.TotalRecipients, b.SentCount, b.DeliveredCount, b.FailedCount, b.EstimatedCost, b.ActualCost, b.Currency, b.Status.ToString(), b.QueuedAt, b.StartedAt, b.CompletedAt, b.CostEstimateJson, b.CreatedAt)).ToList();
        return Ok(dtos);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("batches/{id:long}")]
    public async Task<IActionResult> GetBatch(long id, CancellationToken ct)
    {
        var batch = await _db.Set<MessageBatch>().FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId && !b.IsDeleted, ct);
        if (batch == null) return NotFound();
        return Ok(new MessageBatchDto(batch.Id, batch.BatchNumber, batch.Title, batch.TemplateId, batch.Channel.ToString(), batch.AudienceType.ToString(), batch.Body, batch.Subject, batch.TotalRecipients, batch.SentCount, batch.DeliveredCount, batch.FailedCount, batch.EstimatedCost, batch.ActualCost, batch.Currency, batch.Status.ToString(), batch.QueuedAt, batch.StartedAt, batch.CompletedAt, batch.CostEstimateJson, batch.CreatedAt));
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("batches/{id:long}/logs")]
    public async Task<IActionResult> GetLogs(long id, CancellationToken ct)
    {
        var logs = await _db.Set<MessageDeliveryLog>().Where(l => l.BatchId == id && l.TenantId == TenantId && !l.IsDeleted).OrderBy(l => l.Id).Take(200).ToListAsync(ct);
        var dtos = logs.Select(l => new DeliveryLogDto(l.Id, l.BatchId, l.GuardianId, l.StudentId, l.RecipientName, l.RecipientAddress, l.Channel.ToString(), l.Status.ToString(), l.Provider, l.ProviderReference, l.Cost, l.Currency, l.RenderedBody, l.RetryCount, l.LastAttemptAt, l.DeliveredAt, l.FailureReason, l.IsOptedOut)).ToList();
        return Ok(dtos);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var usage = await _db.Set<TenantMessagingUsage>().FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Year == now.Year && u.Month == now.Month && !u.IsDeleted, ct);
        if (usage == null) return Ok(new UsageDto(now.Year, now.Month, 0, 0, 0m, 0m, 1000, 5000, 1000, 5000, false, null));

        var smsRemaining = usage.SmsLimit - usage.SmsCount;
        var emailRemaining = usage.EmailLimit - usage.EmailCount;
        var nearCap = smsRemaining < usage.SmsLimit * 0.1 || emailRemaining < usage.EmailLimit * 0.1;
        string? warning = null;
        if (nearCap)
        {
            warning = $"Warning: Near cap - SMS {usage.SmsCount}/{usage.SmsLimit} remaining {smsRemaining}, Email {usage.EmailCount}/{usage.EmailLimit} remaining {emailRemaining}";
        }

        return Ok(new UsageDto(usage.Year, usage.Month, usage.SmsCount, usage.EmailCount, usage.SmsCost, usage.EmailCost, usage.SmsLimit, usage.EmailLimit, smsRemaining, emailRemaining, nearCap, warning));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("opt-out")]
    public async Task<IActionResult> OptOut([FromBody] OptOutRequest req, CancellationToken ct)
    {
        var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == TenantId && p.GuardianId == req.GuardianId && !p.IsDeleted, ct);
        if (pref == null)
        {
            pref = new GuardianContactPreference { TenantId = TenantId, GuardianId = req.GuardianId, SmsOptIn = true, EmailOptIn = true };
            _db.Set<GuardianContactPreference>().Add(pref);
        }

        if (req.Channel.ToLower() == "sms")
        {
            pref.SmsOptOut = true;
            pref.SmsOptOutAt = DateTime.UtcNow;
            pref.SmsOptIn = false;
        }
        else
        {
            pref.EmailOptOut = true;
            pref.EmailOptOutAt = DateTime.UtcNow;
            pref.EmailOptIn = false;
        }
        pref.OptOutReason = req.Reason;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = $"Guardian {req.GuardianId} opted out {req.Channel}, always honoured" });
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("contact-preferences")]
    public async Task<IActionResult> UpdatePreference([FromBody] ContactPreferenceDto req, CancellationToken ct)
    {
        var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == TenantId && p.GuardianId == req.GuardianId && !p.IsDeleted, ct);
        if (pref == null)
        {
            pref = new GuardianContactPreference { TenantId = TenantId, GuardianId = req.GuardianId };
            _db.Set<GuardianContactPreference>().Add(pref);
        }
        pref.SmsOptIn = req.SmsOptIn && !req.SmsOptOut;
        pref.EmailOptIn = req.EmailOptIn && !req.EmailOptOut;
        pref.SmsOptOut = req.SmsOptOut;
        pref.EmailOptOut = req.EmailOptOut;
        pref.PreferredLanguage = req.PreferredLanguage;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Preference updated" });
    }

    private static AudienceType MapAudience(string type) => type.ToLower() switch
    {
        "class" => AudienceType.Class,
        "stream" => AudienceType.Stream,
        "year_group" => AudienceType.YearGroup,
        "all_guardians" => AudienceType.AllGuardians,
        "arrears_over_x" => AudienceType.ArrearsOverX,
        "absent_today" => AudienceType.AbsentToday,
        "manual" => AudienceType.Manual,
        _ => AudienceType.DynamicList
    };
}
