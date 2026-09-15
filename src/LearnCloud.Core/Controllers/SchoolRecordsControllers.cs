using LearnCloud.Core.DTOs;
using LearnCloud.Core.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LearnCloud.Core.Controllers;

// Core school records API. Business errors are InvalidOperationExceptions that the API's
// exception handler maps: "... not found" 404, "... already exists" 409, anything else 400.
// Request shapes are validated before the action runs (422).

[ApiController]
[Authorize]
[EnableRateLimiting("api_general")]
public abstract class SchoolRecordsController : ControllerBase
{
    private readonly ITenantContext _tenantContext;

    protected SchoolRecordsController(ITenantContext tenantContext) => _tenantContext = tenantContext;

    protected long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant context");
    protected long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");
}

[Route("api/academic")]
public class AcademicCalendarController : SchoolRecordsController
{
    private readonly IAcademicCalendarService _calendar;

    public AcademicCalendarController(IAcademicCalendarService calendar, ITenantContext tenantContext) : base(tenantContext) => _calendar = calendar;

    [HttpGet("years")]
    [Authorize(Roles = SchoolRecordRoles.CalendarReaders)]
    public async Task<ActionResult<List<CalendarYearDto>>> ListYears(CancellationToken ct) => await _calendar.ListYearsAsync(TenantId, ct);

    [HttpGet("current")]
    [Authorize(Roles = SchoolRecordRoles.CalendarReaders)]
    public async Task<ActionResult<CurrentCalendarDto>> Current(CancellationToken ct) => await _calendar.GetCurrentAsync(TenantId, ct);

    [HttpGet("years/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.CalendarReaders)]
    public async Task<ActionResult<CalendarYearDto>> GetYear(long id, CancellationToken ct) => await _calendar.GetYearAsync(TenantId, id, ct);

    [HttpPost("years")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<IActionResult> CreateYear([FromBody] CreateAcademicYearRequest req, CancellationToken ct)
    {
        var year = await _calendar.CreateYearAsync(TenantId, UserId, req, ct);
        return CreatedAtAction(nameof(GetYear), new { id = year.Id }, year);
    }

    [HttpPut("years/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<ActionResult<CalendarYearDto>> UpdateYear(long id, [FromBody] UpdateAcademicYearRequest req, CancellationToken ct) =>
        await _calendar.UpdateYearAsync(TenantId, UserId, id, req, ct);

    [HttpPost("years/{id:long}/set-current")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<ActionResult<CalendarYearDto>> SetCurrentYear(long id, CancellationToken ct) =>
        await _calendar.SetCurrentYearAsync(TenantId, UserId, id, ct);

    [HttpDelete("years/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.Administrators)]
    public async Task<IActionResult> DeleteYear(long id, CancellationToken ct)
    {
        await _calendar.DeleteYearAsync(TenantId, UserId, id, ct);
        return NoContent();
    }

    [HttpPost("years/{yearId:long}/terms")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<IActionResult> CreateTerm(long yearId, [FromBody] CreateTermRequest req, CancellationToken ct)
    {
        var term = await _calendar.CreateTermAsync(TenantId, UserId, yearId, req, ct);
        return CreatedAtAction(nameof(GetYear), new { id = yearId }, term);
    }

    [HttpPut("terms/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<ActionResult<CalendarTermDto>> UpdateTerm(long id, [FromBody] UpdateTermRequest req, CancellationToken ct) =>
        await _calendar.UpdateTermAsync(TenantId, UserId, id, req, ct);

    [HttpPost("terms/{id:long}/set-current")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<ActionResult<CalendarTermDto>> SetCurrentTerm(long id, CancellationToken ct) =>
        await _calendar.SetCurrentTermAsync(TenantId, UserId, id, ct);

    [HttpDelete("terms/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.Administrators)]
    public async Task<IActionResult> DeleteTerm(long id, CancellationToken ct)
    {
        await _calendar.DeleteTermAsync(TenantId, UserId, id, ct);
        return NoContent();
    }
}

[Route("api/academic")]
public class GradesController : SchoolRecordsController
{
    private readonly IClassStructureService _classes;

    public GradesController(IClassStructureService classes, ITenantContext tenantContext) : base(tenantContext) => _classes = classes;

    /// <summary>Grades with streams for an academic year (default: the current year).</summary>
    [HttpGet("grades")]
    [Authorize(Roles = SchoolRecordRoles.CalendarReaders)]
    public async Task<ActionResult<List<GradeDto>>> ListGrades([FromQuery] long? academicYearId, CancellationToken ct) =>
        await _classes.ListGradesAsync(TenantId, academicYearId, ct);

    [HttpGet("grades/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.CalendarReaders)]
    public async Task<ActionResult<GradeDto>> GetGrade(long id, CancellationToken ct) => await _classes.GetGradeAsync(TenantId, id, ct);

    [HttpPost("grades")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<IActionResult> CreateGrade([FromBody] CreateGradeRequest req, CancellationToken ct)
    {
        var grade = await _classes.CreateGradeAsync(TenantId, UserId, req, ct);
        return CreatedAtAction(nameof(GetGrade), new { id = grade.Id }, grade);
    }

    [HttpPut("grades/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<ActionResult<GradeDto>> UpdateGrade(long id, [FromBody] UpdateGradeRequest req, CancellationToken ct) =>
        await _classes.UpdateGradeAsync(TenantId, UserId, id, req, ct);

    [HttpDelete("grades/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.Administrators)]
    public async Task<IActionResult> DeleteGrade(long id, CancellationToken ct)
    {
        await _classes.DeleteGradeAsync(TenantId, UserId, id, ct);
        return NoContent();
    }

    [HttpPost("grades/{gradeId:long}/streams")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<IActionResult> CreateStream(long gradeId, [FromBody] CreateStreamRequest req, CancellationToken ct)
    {
        var stream = await _classes.CreateStreamAsync(TenantId, UserId, gradeId, req, ct);
        return CreatedAtAction(nameof(GetGrade), new { id = gradeId }, stream);
    }

    [HttpPut("streams/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.CalendarWriters)]
    public async Task<ActionResult<StreamDto>> UpdateStream(long id, [FromBody] UpdateStreamRequest req, CancellationToken ct) =>
        await _classes.UpdateStreamAsync(TenantId, UserId, id, req, ct);

    [HttpDelete("streams/{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.Administrators)]
    public async Task<IActionResult> DeleteStream(long id, CancellationToken ct)
    {
        await _classes.DeleteStreamAsync(TenantId, UserId, id, ct);
        return NoContent();
    }
}

[Route("api/students")]
public class StudentsController : SchoolRecordsController
{
    private readonly IStudentRecordsService _students;

    public StudentsController(IStudentRecordsService students, ITenantContext tenantContext) : base(tenantContext) => _students = students;

    [HttpGet]
    [Authorize(Roles = SchoolRecordRoles.RecordReaders)]
    public async Task<ActionResult<PagedResult<StudentListItemDto>>> List([FromQuery] StudentListRequest req, CancellationToken ct) =>
        await _students.ListAsync(TenantId, req, ct);

    [HttpGet("export")]
    [Authorize(Roles = SchoolRecordRoles.RecordReaders)]
    public async Task<IActionResult> Export([FromQuery] StudentListRequest req, CancellationToken ct) =>
        File(await _students.ExportCsvAsync(TenantId, req, ct), "text/csv; charset=utf-8", $"students_{DateTime.UtcNow:yyyyMMdd}.csv");

    [HttpGet("{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.RecordReaders)]
    public async Task<ActionResult<StudentDetailDto>> Get(long id, CancellationToken ct) => await _students.GetAsync(TenantId, id, ct);

    [HttpPost]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest req, CancellationToken ct)
    {
        var student = await _students.CreateAsync(TenantId, UserId, req, ct);
        return CreatedAtAction(nameof(Get), new { id = student.Id }, student);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<ActionResult<StudentDetailDto>> Update(long id, [FromBody] UpdateStudentRequest req, CancellationToken ct) =>
        await _students.UpdateAsync(TenantId, UserId, id, req, ct);

    [HttpDelete("{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.Administrators)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _students.DeleteAsync(TenantId, UserId, id, ct);
        return NoContent();
    }

    /// <summary>Moves the student to another class, into a later year, or readmits them.</summary>
    [HttpPost("{id:long}/enrolments")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<ActionResult<StudentDetailDto>> ChangeEnrolment(long id, [FromBody] ChangeEnrolmentRequest req, CancellationToken ct) =>
        await _students.ChangeEnrolmentAsync(TenantId, UserId, id, req, ct);

    /// <summary>Ends the current enrolment: withdrawn, transferred_out or graduated.</summary>
    [HttpPost("{id:long}/exit")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<ActionResult<StudentDetailDto>> Exit(long id, [FromBody] ExitStudentRequest req, CancellationToken ct) =>
        await _students.ExitAsync(TenantId, UserId, id, req, ct);

    [HttpGet("{id:long}/guardians")]
    [Authorize(Roles = SchoolRecordRoles.RecordReaders)]
    public async Task<ActionResult<List<StudentGuardianDto>>> ListGuardians(long id, CancellationToken ct) =>
        await _students.ListGuardiansAsync(TenantId, id, ct);

    [HttpPost("{id:long}/guardians")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<IActionResult> LinkGuardian(long id, [FromBody] LinkGuardianRequest req, CancellationToken ct)
    {
        var link = await _students.LinkGuardianAsync(TenantId, UserId, id, req, ct);
        return CreatedAtAction(nameof(ListGuardians), new { id }, link);
    }

    [HttpPut("{id:long}/guardians/{linkId:long}")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<ActionResult<StudentGuardianDto>> UpdateGuardianLink(long id, long linkId, [FromBody] UpdateGuardianLinkRequest req, CancellationToken ct) =>
        await _students.UpdateGuardianLinkAsync(TenantId, UserId, id, linkId, req, ct);

    [HttpDelete("{id:long}/guardians/{linkId:long}")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<IActionResult> UnlinkGuardian(long id, long linkId, CancellationToken ct)
    {
        await _students.UnlinkGuardianAsync(TenantId, UserId, id, linkId, ct);
        return NoContent();
    }
}

[Route("api/guardians")]
public class GuardiansController : SchoolRecordsController
{
    private readonly IGuardianService _guardians;

    public GuardiansController(IGuardianService guardians, ITenantContext tenantContext) : base(tenantContext) => _guardians = guardians;

    [HttpGet]
    [Authorize(Roles = SchoolRecordRoles.RecordReaders)]
    public async Task<ActionResult<PagedResult<GuardianListItemDto>>> List([FromQuery] GuardianListRequest req, CancellationToken ct) =>
        await _guardians.ListAsync(TenantId, req, ct);

    [HttpGet("{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.RecordReaders)]
    public async Task<ActionResult<GuardianDetailDto>> Get(long id, CancellationToken ct) => await _guardians.GetAsync(TenantId, id, ct);

    [HttpPost]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<IActionResult> Create([FromBody] GuardianInput req, CancellationToken ct)
    {
        var guardian = await _guardians.CreateAsync(TenantId, UserId, req, ct);
        return CreatedAtAction(nameof(Get), new { id = guardian.Id }, guardian);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.RecordWriters)]
    public async Task<ActionResult<GuardianDetailDto>> Update(long id, [FromBody] GuardianInput req, CancellationToken ct) =>
        await _guardians.UpdateAsync(TenantId, UserId, id, req, ct);

    [HttpDelete("{id:long}")]
    [Authorize(Roles = SchoolRecordRoles.Administrators)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _guardians.DeleteAsync(TenantId, UserId, id, ct);
        return NoContent();
    }
}
