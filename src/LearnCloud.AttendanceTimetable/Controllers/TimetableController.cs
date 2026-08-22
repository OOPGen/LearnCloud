using LearnCloud.AttendanceTimetable.DTOs;
using LearnCloud.AttendanceTimetable.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.AttendanceTimetable.Controllers;

[ApiController]
[Route("api/timetable")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class TimetableController : ControllerBase
{
    private readonly ITimetableService _timetable;
    private readonly ITenantContext _tenantContext;

    public TimetableController(ITimetableService timetable, ITenantContext tenantContext)
    {
        _timetable = timetable;
        _tenantContext = tenantContext;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant context");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Periods defined per tenant with times and break slots
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("periods")]
    public async Task<IActionResult> GetPeriods([FromQuery] long? academicYearId, CancellationToken ct)
    {
        var periods = await _timetable.GetPeriodsAsync(TenantId, academicYearId, ct);
        return Ok(periods);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("periods")]
    public async Task<IActionResult> CreatePeriod([FromBody] CreatePeriodRequest req, CancellationToken ct)
    {
        var period = await _timetable.CreatePeriodAsync(TenantId, UserId, req, ct);
        return Ok(period);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("periods/bulk")]
    public async Task<IActionResult> BulkPeriods([FromBody] BulkPeriodsRequest req, CancellationToken ct)
    {
        await _timetable.BulkSavePeriodsAsync(TenantId, UserId, req, ct);
        var periods = await _timetable.GetPeriodsAsync(TenantId, null, ct);
        return Ok(periods);
    }

    // Timetable CRUD effective dated
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost]
    public async Task<IActionResult> CreateTimetable([FromBody] CreateTimetableRequest req, CancellationToken ct)
    {
        var timetable = await _timetable.CreateTimetableAsync(TenantId, UserId, req, ct);
        return Ok(timetable);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet]
    public async Task<IActionResult> ListTimetables([FromQuery] long academicYearId, [FromQuery] long termId, CancellationToken ct)
    {
        var list = await _timetable.ListTimetablesAsync(TenantId, academicYearId, termId, ct);
        return Ok(list);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("{timetableId:long}")]
    public async Task<IActionResult> GetTimetable(long timetableId, CancellationToken ct)
    {
        var t = await _timetable.GetTimetableAsync(TenantId, timetableId, ct);
        return Ok(t);
    }

    // Weekly grid editor assigning subject and teacher to a class period
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("{timetableId:long}/slots")]
    public async Task<IActionResult> CreateSlot(long timetableId, [FromBody] CreateSlotRequest req, CancellationToken ct)
    {
        try
        {
            var slot = await _timetable.CreateSlotAsync(TenantId, UserId, timetableId, req, ct);
            return Ok(slot);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Clash"))
        {
            // Explain clash in plain language, do not silently refuse
            return Conflict(new { message = ex.Message, code = "CLASH_DETECTED" });
        }
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("{timetableId:long}/slots/check-clash")]
    public async Task<IActionResult> CheckClash(long timetableId, [FromBody] CreateSlotRequest req, CancellationToken ct)
    {
        var result = await _timetable.CheckClashAsync(TenantId, timetableId, req, null, ct);
        return Ok(result);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("{timetableId:long}/slots/bulk")]
    public async Task<IActionResult> BulkSlots(long timetableId, [FromBody] BulkSlotsRequest req, CancellationToken ct)
    {
        var result = await _timetable.BulkCreateSlotsAsync(TenantId, UserId, timetableId, req, ct);
        if (result.HasClash)
            return Ok(new { message = "Some slots had clashes, others saved", clashes = result.Clashes, code = "PARTIAL_CLASH" });
        return Ok(result);
    }

    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [HttpDelete("{timetableId:long}/slots/{slotId:long}")]
    public async Task<IActionResult> DeleteSlot(long timetableId, long slotId, CancellationToken ct)
    {
        await _timetable.DeleteSlotAsync(TenantId, timetableId, slotId, ct);
        return Ok(new { message = "Deleted" });
    }

    // Views by class and by teacher, both printable
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("view")]
    public async Task<IActionResult> GetGrid([FromQuery] TimetableViewRequest req, CancellationToken ct)
    {
        var grid = await _timetable.GetGridAsync(TenantId, req, ct);
        return Ok(grid);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("view/class")]
    public async Task<IActionResult> GetByClass([FromQuery] long gradeId, [FromQuery] long streamId, [FromQuery] long academicYearId, [FromQuery] long termId, [FromQuery] DateTime? effectiveDate, CancellationToken ct)
    {
        var grid = await _timetable.GetByClassAsync(TenantId, gradeId, streamId, academicYearId, termId, effectiveDate, ct);
        return Ok(grid);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("view/teacher")]
    public async Task<IActionResult> GetByTeacher([FromQuery] long teacherStaffId, [FromQuery] long academicYearId, [FromQuery] long termId, [FromQuery] DateTime? effectiveDate, CancellationToken ct)
    {
        var grid = await _timetable.GetByTeacherAsync(TenantId, teacherStaffId, academicYearId, termId, effectiveDate, ct);
        return Ok(grid);
    }

    // Printable HTML for class and teacher
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("view/class/html")]
    public async Task<IActionResult> GetByClassHtml([FromQuery] long gradeId, [FromQuery] long streamId, [FromQuery] long academicYearId, [FromQuery] long termId, [FromQuery] DateTime? effectiveDate, CancellationToken ct)
    {
        var grid = await _timetable.GetByClassAsync(TenantId, gradeId, streamId, academicYearId, termId, effectiveDate, ct);
        var html = GenerateGridHtml(grid, $"Class Timetable {gradeId} {streamId}");
        return Content(html, "text/html");
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("view/teacher/html")]
    public async Task<IActionResult> GetByTeacherHtml([FromQuery] long teacherStaffId, [FromQuery] long academicYearId, [FromQuery] long termId, [FromQuery] DateTime? effectiveDate, CancellationToken ct)
    {
        var grid = await _timetable.GetByTeacherAsync(TenantId, teacherStaffId, academicYearId, termId, effectiveDate, ct);
        var html = GenerateGridHtml(grid, $"Teacher Timetable {teacherStaffId}");
        return Content(html, "text/html");
    }

    private string GenerateGridHtml(TimetableGridDto grid, string title)
    {
        var periodHeaders = string.Join("", grid.Periods.Where(p=>!p.IsBreak).Select(p=>$"<th>{p.Name}<br><small>{p.StartTime}-{p.EndTime}</small></th>"));
        var rows = string.Join("", grid.Days.Select(day =>
        {
            var cells = string.Join("", grid.Periods.Where(p=>!p.IsBreak).Select(period =>
            {
                var slot = day.Slots.FirstOrDefault(s=>s.PeriodNumber==period.PeriodNumber);
                var content = slot != null ? $"{slot.SubjectName}<br>{slot.TeacherName}<br>{slot.RoomName}" : "";
                return $"<td>{content}</td>";
            }));
            return $"<tr><td><strong>{day.DayName}</strong></td>{cells}</tr>";
        }));
        return $"<html><head><style>table{{border-collapse:collapse;width:100%}} th,td{{border:1px solid black;padding:6px;font-size:12px}} @media print{{body{{margin:0}}}}</style></head><body><h1>{title} - {grid.TimetableName}</h1><table><tr><th>Day</th>{periodHeaders}</tr>{rows}</table><p>Printed from LearnCloud</p></body></html>";
    }
}