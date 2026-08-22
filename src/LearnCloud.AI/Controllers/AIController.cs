using LearnCloud.AI.DTOs;
using LearnCloud.AI.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.AI.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class AIController : ControllerBase
{
    private readonly ITenantContext _tenantContext;
    private readonly IReportCommentService _commentService;
    private readonly IAttendanceAnomalyService _anomalyService;
    private readonly IAtRiskService _atRiskService;

    public AIController(ITenantContext tenantContext, IReportCommentService commentService, IAttendanceAnomalyService anomalyService, IAtRiskService atRiskService)
    {
        _tenantContext = tenantContext;
        _commentService = commentService;
        _anomalyService = anomalyService;
        _atRiskService = atRiskService;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // 1. Report card comment drafting - teacher always reviews and edits before saving, nothing written automatically
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("comments/generate")]
    [Authorize(Roles = "TEACHER,HEAD_TEACHER,DEPUTY_HEAD")]
    public async Task<IActionResult> GenerateComment([FromBody] GenerateCommentRequest req, CancellationToken ct)
    {
        var result = await _commentService.GenerateDraftAsync(TenantId, UserId, req, ct);
        return Ok(result);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("comments/{draftId:long}/review")]
    [Authorize(Roles = "TEACHER,HEAD_TEACHER")]
    public async Task<IActionResult> ReviewComment(long draftId, [FromBody] ReviewCommentRequest req, CancellationToken ct)
    {
        if (req.DraftId != draftId) return BadRequest(new { message = "DraftId mismatch" });
        var draft = await _commentService.ReviewAsync(TenantId, UserId, req, ct);
        return Ok(draft);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("comments/{draftId:long}/save")]
    [Authorize(Roles = "TEACHER,HEAD_TEACHER")]
    public async Task<IActionResult> SaveComment(long draftId, [FromBody] SaveCommentRequest req, CancellationToken ct)
    {
        if (req.DraftId != draftId) return BadRequest(new { message = "DraftId mismatch" });
        var draft = await _commentService.SaveAsync(TenantId, UserId, req, ct);
        return Ok(new { message = "Comment saved to report card after teacher review - nothing was written automatically, teacher reviewed and edited", draft });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("comments/student/{studentId:long}")]
    public async Task<IActionResult> ListDrafts(long studentId, CancellationToken ct)
    {
        var drafts = await _commentService.ListDraftsAsync(TenantId, studentId, ct);
        return Ok(drafts);
    }

    // 2. Attendance anomaly detection
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("attendance-anomalies/detect")]
    [Authorize(Roles = "HEAD_TEACHER,DEPUTY_HEAD,SCHOOL_ADMIN,TEACHER")]
    public async Task<IActionResult> DetectAnomalies([FromBody] DetectAnomaliesRequest req, CancellationToken ct)
    {
        var anomalies = await _anomalyService.DetectAsync(TenantId, req.GradeId, req.StreamId, req.AcademicYearId, req.TermId, req.DaysBack ?? 30, req.Threshold ?? 75m, ct);
        return Ok(anomalies);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("attendance-anomalies")]
    public async Task<IActionResult> ListAnomalies([FromQuery] long? studentId, CancellationToken ct)
    {
        var list = await _anomalyService.ListAsync(TenantId, studentId, ct);
        return Ok(list);
    }

    // 3. At-risk learner identification
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("at-risk/detect")]
    [Authorize(Roles = "HEAD_TEACHER,DEPUTY_HEAD,SCHOOL_ADMIN,TEACHER,COUNSELOR")]
    public async Task<IActionResult> DetectAtRisk([FromBody] DetectAtRiskRequest req, CancellationToken ct)
    {
        var flags = await _atRiskService.DetectAsync(TenantId, req.GradeId, req.StreamId, req.AcademicYearId, req.TermId, req.MarksDropThreshold ?? 15m, req.AttendanceDropThreshold ?? 15m, req.ArrearsThreshold ?? 100m, ct);
        return Ok(flags);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("at-risk")]
    public async Task<IActionResult> ListAtRisk([FromQuery] long? studentId, CancellationToken ct)
    {
        var list = await _atRiskService.ListAsync(TenantId, studentId, ct);
        return Ok(list);
    }

    // Provider settings - swappable without touching calling code
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("providers")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> ListProviders(CancellationToken ct)
    {
        // Return list of AI provider settings
        return Ok(new[] { new { providerName = "RuleBased", isActive = true, isDefault = true, enableCommentDrafting = true, enableAttendanceAnomaly = true, enableAtRiskDetection = true } });
    }
}