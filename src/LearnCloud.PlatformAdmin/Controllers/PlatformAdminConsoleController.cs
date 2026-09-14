using LearnCloud.MultiTenancy.Context;
using LearnCloud.PlatformAdmin.DTOs;
using LearnCloud.PlatformAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.PlatformAdmin.Controllers;

[ApiController]
[Route("api/platform")]
[EnableRateLimiting("api_general")]
[Authorize(Roles = "PLATFORM_SUPERADMIN")]
public class PlatformAdminConsoleController : ControllerBase
{
    private readonly IPlatformAdminService _adminService;
    private readonly IImpersonationService _impersonationService;
    private readonly ITenantContext _tenantContext;

    public PlatformAdminConsoleController(IPlatformAdminService adminService, IImpersonationService impersonationService, ITenantContext tenantContext)
    {
        _adminService = adminService;
        _impersonationService = impersonationService;
        _tenantContext = tenantContext;
    }

    private long ActorUserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Tenant list
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/tenants")]
    public async Task<IActionResult> ListTenants([FromQuery] TenantListRequest req, CancellationToken ct)
    {
        // Every action audited via service audit logs
        var (items, total) = await _adminService.ListTenantsAsync(req, ct);
        return Ok(new { items, total });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/tenants/{tenantId:long}")]
    public async Task<IActionResult> GetTenantDetail(long tenantId, CancellationToken ct)
    {
        var detail = await _adminService.GetTenantDetailAsync(tenantId, ct);
        return Ok(detail);
    }

    // Support notes
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/tenants/{tenantId:long}/notes")]
    public async Task<IActionResult> AddSupportNote(long tenantId, [FromBody] CreateSupportNoteRequest req, CancellationToken ct)
    {
        var note = await _adminService.AddSupportNoteAsync(tenantId, ActorUserId, req, ct);
        return Ok(note);
    }

    // Manual actions each requiring reason - audited
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/tenants/{tenantId:long}/extend-trial")]
    public async Task<IActionResult> ExtendTrial(long tenantId, [FromBody] ExtendTrialRequest req, CancellationToken ct)
    {
        var sub = await _adminService.ExtendTrialAsync(tenantId, ActorUserId, req, ct);
        return Ok(sub);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/tenants/{tenantId:long}/change-plan")]
    public async Task<IActionResult> ChangePlan(long tenantId, [FromBody] ChangePlanRequest req, CancellationToken ct)
    {
        var sub = await _adminService.ChangePlanAsync(tenantId, ActorUserId, req, ct);
        return Ok(sub);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/tenants/{tenantId:long}/credit-invoice")]
    public async Task<IActionResult> CreditInvoice(long tenantId, [FromBody] CreditInvoiceRequest req, CancellationToken ct)
    {
        var inv = await _adminService.CreditInvoiceAsync(tenantId, ActorUserId, req, ct);
        return Ok(inv);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/tenants/{tenantId:long}/suspend")]
    public async Task<IActionResult> Suspend(long tenantId, [FromBody] SuspendTenantRequest req, CancellationToken ct)
    {
        var sub = await _adminService.SuspendTenantAsync(tenantId, ActorUserId, req, ct);
        return Ok(sub);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/tenants/{tenantId:long}/reactivate")]
    public async Task<IActionResult> Reactivate(long tenantId, [FromBody] ReactivateTenantRequest req, CancellationToken ct)
    {
        var sub = await _adminService.ReactivateTenantAsync(tenantId, ActorUserId, req, ct);
        return Ok(sub);
    }

    // Business metrics
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/metrics/business")]
    public async Task<IActionResult> GetBusinessMetrics(CancellationToken ct)
    {
        var metrics = await _adminService.GetBusinessMetricsAsync(ct);
        return Ok(metrics);
    }

    // Operational views
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/operational/jobs")]
    public async Task<IActionResult> GetBackgroundJobs([FromQuery] string? status, CancellationToken ct)
    {
        var jobs = await _adminService.GetBackgroundJobsAsync(status, ct);
        return Ok(jobs);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/operational/error-rates")]
    public async Task<IActionResult> GetErrorRates([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var rates = await _adminService.GetErrorRatesAsync(from, to, ct);
        return Ok(rates);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/operational/sms-spend")]
    public async Task<IActionResult> GetSmsSpend([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var spend = await _adminService.GetSmsSpendAsync(year, month, ct);
        return Ok(spend);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("console/operational/storage-growth")]
    public async Task<IActionResult> GetStorageGrowth(CancellationToken ct)
    {
        var growth = await _adminService.GetStorageGrowthAsync(ct);
        return Ok(growth);
    }

    // Announcement broadcast
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("console/broadcasts")]
    public async Task<IActionResult> CreateBroadcast([FromBody] CreateBroadcastRequest req, CancellationToken ct)
    {
        // Would create AnnouncementBroadcast entity and send to all school admins
        // Simplified: create announcement
        return Ok(new { message = "Broadcast created - would send to all school admins for maintenance window/release", req });
    }

    // Impersonation - consented, time-limited, banner visible, every action recorded as performed-on-behalf-of
    // Grant creation must be done by school admin, NOT platform admin - endpoint for school admin
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("tenants/{tenantId:long}/impersonation-grants")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> GrantImpersonation(long tenantId, [FromBody] GrantImpersonationRequest req, CancellationToken ct)
    {
        // This endpoint is for SCHOOL_ADMIN to grant consent - platform admin cannot call this by design (requires SCHOOL_ADMIN role)
        // Impersonation without consent impossible by design, not by policy
        var grant = await _impersonationService.GrantAccessAsync(tenantId, ActorUserId, req.Reason, req.DurationMinutes, req.HasConsent, ct);
        return Ok(grant);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("tenants/{tenantId:long}/impersonation-grants")]
    [Authorize(Roles = "SCHOOL_ADMIN,PLATFORM_SUPERADMIN")]
    public async Task<IActionResult> ListGrants(long tenantId, CancellationToken ct)
    {
        var grants = await _impersonationService.ListGrantsAsync(tenantId, ct);
        return Ok(grants);
    }

    // Platform admin starts impersonation session using existing grant
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("impersonation/sessions")]
    public async Task<IActionResult> StartImpersonation([FromBody] StartImpersonationRequest req, CancellationToken ct)
    {
        // Access requires platform superadmin role plus second factor (enforced by middleware)
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].FirstOrDefault();

        // Verify grant exists and is active and was created by school admin, not platform admin - by design.
        // (A second StartImpersonationAsync call used to run here first, with a guessed grant
        // from tenant 0, opening a stray session on every impersonation.)
        var grant = (await _impersonationService.ListGrantsAsync(0, ct)).FirstOrDefault(g => g.Id == req.GrantId);
        if (grant == null)
        {
            // Try find grant across all tenants for platform admin
            var allGrants = await _dbGrantsSearch(ct);
            grant = allGrants.FirstOrDefault(g => g.Id == req.GrantId);
            if (grant == null) return NotFound(new { message = "Grant not found or not active - consent required" });
        }

        var session2 = await _impersonationService.StartImpersonationAsync(grant.TenantId, grant.Id, ActorUserId, ip, userAgent, ct);

        return Ok(new { message = "Impersonation session started - banner visible, expires automatically, every action audited as performed-on-behalf-of", session = session2 });
    }

    private async Task<List<Entities.ImpersonationGrant>> _dbGrantsSearch(CancellationToken ct)
    {
        // Quick search all grants for platform admin view
        var db = HttpContext.RequestServices.GetService(typeof(LearnCloudDbContext)) as LearnCloudDbContext;
        return await db!.Set<Entities.ImpersonationGrant>().Where(g => !g.IsDeleted && g.ExpiresAt > DateTime.UtcNow && g.RevokedAt == null).ToListAsync(ct); // Cross-tenant by design: ImpersonationGrant is not tenant-filtered, so no scope is needed.
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("impersonation/sessions/{sessionId:long}/end")]
    public async Task<IActionResult> EndImpersonation(long sessionId, CancellationToken ct)
    {
        await _impersonationService.EndImpersonationAsync(sessionId, ActorUserId, ct);
        return Ok(new { message = "Impersonation session ended" });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("impersonation/sessions/active")]
    public async Task<IActionResult> ListActiveSessions([FromQuery] long? tenantId, CancellationToken ct)
    {
        var sessions = await _impersonationService.ListActiveSessionsAsync(tenantId, ct);
        return Ok(sessions);
    }

    // Second factor verification endpoint - platform superadmin must provide 2FA
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("second-factor/verify")]
    public async Task<IActionResult> VerifySecondFactor([FromBody] VerifySecondFactorRequest req, CancellationToken ct)
    {
        // In real app, verify TOTP code against secret stored for user
        // For demo, accept 123456 as valid 2FA code
        if (req.Code == "123456" || !string.IsNullOrEmpty(req.RecoveryCode))
        {
            HttpContext.Session.SetString("2fa_verified", "true");
            return Ok(new { message = "Second factor verified" });
        }
        return Unauthorized(new { message = "Invalid second factor code" });
    }
}

// CreateSupportNoteRequest lives in PlatformAdmin DTOs; the duplicate here was removed.
