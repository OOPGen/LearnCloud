using LearnCloud.MultiTenancy.Context;
using LearnCloud.ParentPortal.DTOs;
using LearnCloud.ParentPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.ParentPortal.Controllers;

[ApiController]
[Route("api/parent")]
[EnableRateLimiting("api_general")]
[Authorize(Roles = "PARENT,GUARDIAN")] // simplified - in real app role PARENT
public class ParentPortalController : ControllerBase
{
    private readonly ITenantContext _tenantContext;
    private readonly IParentAuthorizationService _authz;
    private readonly IParentPortalService _portal;
    private readonly Services.ParentTeacherMessaging.ParentTeacherMessagingService _messagingService;

    public ParentPortalController(ITenantContext tenantContext, IParentAuthorizationService authz, IParentPortalService portal, Services.ParentTeacherMessaging.ParentTeacherMessagingService messagingService)
    {
        _tenantContext = tenantContext;
        _authz = authz;
        _portal = portal;
        _messagingService = messagingService;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    private async Task<long> GetGuardianIdAsync(CancellationToken ct)
    {
        return await _authz.GetGuardianIdAsync(TenantId, UserId, ct);
    }

    // Login is via /api/auth/login - invitation flow school triggers
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("invitations")]
    [Authorize(Roles = "SCHOOL_ADMIN,REGISTRAR,HEAD_TEACHER")]
    public async Task<IActionResult> InviteGuardian([FromBody] InviteGuardianRequest req, CancellationToken ct)
    {
        var invitation = await _portal.InviteGuardianAsync(TenantId, UserId, req, ct);
        return Ok(invitation);
    }

    [AllowAnonymous]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("accept-invitation")]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest req, CancellationToken ct)
    {
        await _portal.AcceptInvitationAsync(req.Token, req.Email, req.NewPassword, ct);
        return Ok(new { message = "Invitation accepted, account created, you can now login" });
    }

    // Child switcher - account links to one or more children, potentially different classes
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children")]
    public async Task<IActionResult> GetChildren(CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var children = await _portal.GetChildrenAsync(TenantId, guardianId, ct);
        return Ok(children);
    }

    // Home screen per child
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/home")]
    public async Task<IActionResult> GetChildHome(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var home = await _portal.GetChildHomeAsync(TenantId, guardianId, studentId, ct);
        return Ok(home);
    }

    // Fees
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/fees/statement")]
    public async Task<IActionResult> GetStatement(long studentId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var stmt = await _portal.GetStatementAsync(TenantId, guardianId, studentId, from, to, ct);
        return Ok(stmt);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/fees/invoices")]
    public async Task<IActionResult> GetInvoices(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var invoices = await _portal.GetInvoiceHistoryAsync(TenantId, guardianId, studentId, ct);
        return Ok(invoices);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/fees/receipts")]
    public async Task<IActionResult> GetReceipts(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var receipts = await _portal.GetReceiptsAsync(TenantId, guardianId, studentId, ct);
        return Ok(receipts);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/fees/statement/pdf")]
    public async Task<IActionResult> GetStatementPdf(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var stmt = await _portal.GetStatementAsync(TenantId, guardianId, studentId, null, null, ct);
        // Generate simple HTML PDF for download
        var html = $"<html><body><h1>Statement {stmt.StudentName} {stmt.StudentNumber}</h1><p>Balance {stmt.BalanceDue} {stmt.Currency}</p><table border='1'><tr><th>Date</th><th>Type</th><th>Number</th><th>Debit</th><th>Credit</th><th>Balance</th></tr>{string.Join("", stmt.Lines.Select(l => $"<tr><td>{l.Date:dd/MM/yyyy}</td><td>{l.Type}</td><td>{l.Number}</td><td>{l.Debit}</td><td>{l.Credit}</td><td>{l.Balance}</td></tr>"))}</table></body></html>";
        return Content(html, "text/html");
    }

    // Attendance detail
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/attendance")]
    public async Task<IActionResult> GetAttendanceDetail(long studentId, [FromQuery] long? academicYearId, [FromQuery] long? termId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var detail = await _portal.GetAttendanceDetailAsync(TenantId, guardianId, studentId, academicYearId, termId, ct);
        return Ok(detail);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/attendance/summary")]
    public async Task<IActionResult> GetAttendanceSummary(long studentId, [FromQuery] long academicYearId, [FromQuery] long termId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var summary = await _portal.GetAttendanceSummaryAsync(TenantId, guardianId, studentId, academicYearId, termId, ct);
        return Ok(summary);
    }

    // Results published only
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/results")]
    public async Task<IActionResult> GetResults(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var results = await _portal.GetPublishedReportCardsAsync(TenantId, guardianId, studentId, ct);
        return Ok(results);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("report-cards/{reportCardId:long}")]
    public async Task<IActionResult> GetReportCardDetail(long reportCardId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var detail = await _portal.GetReportCardDetailAsync(TenantId, guardianId, reportCardId, ct);
        return Ok(detail);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("report-cards/{reportCardId:long}/pdf")]
    public async Task<IActionResult> GetReportCardPdf(long reportCardId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var detail = await _portal.GetReportCardDetailAsync(TenantId, guardianId, reportCardId, ct);
        // Return HTML for PDF download - in real app generate PDF via QuestPDF
        var html = $"<html><body><h1>Report Card {detail.Header.TermName}</h1><p>Average {detail.Header.Average} Grade {detail.Header.OverallGrade}</p><p>Position {detail.Header.PositionDisplay}</p></body></html>";
        return Content(html, "text/html");
    }

    // Notices and homework
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/notices")]
    public async Task<IActionResult> GetNotices(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var notices = await _portal.GetNoticesAsync(TenantId, guardianId, studentId, ct);
        return Ok(notices);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/homework")]
    public async Task<IActionResult> GetHomework(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var hw = await _portal.GetHomeworkAsync(TenantId, guardianId, studentId, ct);
        return Ok(hw);
    }

    // Message to class teacher, if school enables it, with moderation and rate limiting
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("children/{studentId:long}/messages")]
    public async Task<IActionResult> SendMessageToTeacher(long studentId, [FromBody] SendMessageToTeacherRequest req, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var result = await _messagingService.SendMessageAsync(TenantId, guardianId, UserId, req, ct);
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("children/{studentId:long}/messages")]
    public async Task<IActionResult> GetMessages(long studentId, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var messages = await _messagingService.GetThreadAsync(TenantId, guardianId, studentId, ct);
        return Ok(messages);
    }

    // Profile and contact preferences including SMS opt-out
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var profile = await _portal.GetProfileAsync(TenantId, guardianId, UserId, ct);
        return Ok(profile);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("profile/contact-preferences")]
    public async Task<IActionResult> UpdateContactPreferences([FromBody] UpdateContactPreferencesRequest req, CancellationToken ct)
    {
        var guardianId = await GetGuardianIdAsync(ct);
        var profile = await _portal.UpdateContactPreferencesAsync(TenantId, guardianId, UserId, req, ct);
        return Ok(profile);
    }
}