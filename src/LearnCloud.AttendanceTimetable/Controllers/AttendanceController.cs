using LearnCloud.AttendanceTimetable.DTOs;
using LearnCloud.AttendanceTimetable.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.AttendanceTimetable.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendance;
    private readonly ITenantContext _tenantContext;

    public AttendanceController(IAttendanceService attendance, ITenantContext tenantContext)
    {
        _attendance = attendance;
        _tenantContext = tenantContext;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant context");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");
    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "unknown";

    // GET /api/attendance/settings
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _attendance.GetSettingsAsync(TenantId, ct);
        return Ok(settings);
    }

    // PUT /api/attendance/settings
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateAttendanceSettingsRequest req, CancellationToken ct)
    {
        var settings = await _attendance.UpdateSettingsAsync(TenantId, req, UserId, ct);
        return Ok(settings);
    }

    // GET /api/attendance/register?gradeId=...&streamId=...&attendanceDate=2026-08-02&periodNumber=1&academicYearId=2026&termId=1
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("register")]
    public async Task<IActionResult> GetRegister([FromQuery] GetRegisterRequest req, CancellationToken ct)
    {
        var register = await _attendance.GetRegisterAsync(TenantId, req, ct);
        return Ok(register);
    }

    // POST /api/attendance/mark - bulk mark, autosave every few seconds from frontend
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("mark")]
    public async Task<IActionResult> MarkRegister([FromBody] MarkRegisterRequest req, CancellationToken ct)
    {
        var result = await _attendance.MarkRegisterAsync(TenantId, UserId, req, Ip, ct);
        return Ok(result);
    }

    // GET /api/attendance/summary?gradeId=&streamId=&academicYearId=2026&termId=1&fromDate=&toDate=
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] AttendanceSummaryRequest req, CancellationToken ct)
    {
        var summary = await _attendance.GetSummaryAsync(TenantId, req, ct);
        return Ok(summary);
    }

    // GET /api/attendance/printable/month?gradeId=&streamId=&year=2026&month=8&academicYearId=2026&termId=1
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("printable/month")]
    public async Task<IActionResult> GetPrintableMonth([FromQuery] PrintableMonthRegisterRequest req, CancellationToken ct)
    {
        var printable = await _attendance.GetPrintableMonthAsync(TenantId, req, ct);
        return Ok(printable);
    }

    // GET /api/attendance/printable/month/pdf?gradeId=... returns HTML for print (frontend will handle print CSS)
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("printable/month/html")]
    [Authorize] // SECURITY FIX C5: Was AllowAnonymous with student names - now requires auth
    public async Task<IActionResult> GetPrintableMonthHtml([FromQuery] PrintableMonthRegisterRequest req, CancellationToken ct)
    {
        var printable = await _attendance.GetPrintableMonthAsync(TenantId, req, ct);
        // Return simple HTML for A4 print
        var html = GeneratePrintableHtml(printable);
        return Content(html, "text/html");
    }

    private string GeneratePrintableHtml(PrintableMonthRegisterDto dto)
    {
        var header = $"<h1>{dto.GradeName} {dto.StreamName} - {dto.MonthName} Register</h1>";
        var tableHeader = "<tr><th>Student</th>" + string.Join("", dto.Dates.Select(d => $"<th>{d.Day}</th>")) + "<th>%</th></tr>";
        var rows = string.Join("", dto.Rows.Select(r =>
        {
            var cells = string.Join("", dto.Dates.Select(d => $"<td>{r.AttendanceByDate.GetValueOrDefault(d.ToString("yyyy-MM-dd"), "")}</td>"));
            return $"<tr><td>{r.StudentName} ({r.StudentNumber})</td>{cells}<td></td></tr>";
        }));
        return $"<html><head><style>@media print{{table{{border-collapse:collapse;width:100%}} th,td{{border:1px solid black;padding:4px;font-size:10px}}}}</style></head><body>{header}<table>{tableHeader}{rows}</table><p>Printed from LearnCloud {dto.GradeName} {dto.StreamName} | Key: P=Present A=Absent L=Late S=Sick E=Excused</p></body></html>";
    }
}