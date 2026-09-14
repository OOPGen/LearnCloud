using LearnCloud.Hostel.DTOs;
using LearnCloud.Hostel.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Hostel.Controllers;

[ApiController]
[Route("api/hostel")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class HostelController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHostelService _hostelService;

    public HostelController(LearnCloudDbContext db, ITenantContext tenantContext, IHostelService hostelService)
    {
        _db = db; _tenantContext = tenantContext; _hostelService = hostelService;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Blocks, rooms and beds
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("blocks")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,BOARDING_MASTER")]
    public async Task<IActionResult> CreateBlock([FromBody] CreateBlockRequest req, CancellationToken ct)
    {
        var block = await _hostelService.CreateBlockAsync(TenantId, UserId, req, ct);
        return Ok(block);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks(CancellationToken ct)
    {
        var blocks = await _hostelService.GetBlocksAsync(TenantId, ct);
        return Ok(blocks);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest req, CancellationToken ct)
    {
        var room = await _hostelService.CreateRoomAsync(TenantId, UserId, req, ct);
        return Ok(room);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("beds")]
    public async Task<IActionResult> CreateBed([FromBody] CreateBedRequest req, CancellationToken ct)
    {
        var bed = await _hostelService.CreateBedAsync(TenantId, UserId, req, ct);
        return Ok(bed);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("beds/vacant")]
    public async Task<IActionResult> GetVacantBeds([FromQuery] long? blockId, [FromQuery] string? gender, CancellationToken ct)
    {
        var beds = await _hostelService.GetVacantBedsAsync(TenantId, blockId, gender, ct);
        return Ok(beds);
    }

    // Allocation with conflict detection and waiting list
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("allocations")]
    [Authorize(Roles = "BOARDING_MASTER,SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> AllocateBed([FromBody] AllocateRequest req, CancellationToken ct)
    {
        var allocation = await _hostelService.AllocateBedAsync(TenantId, UserId, req, ct);
        return Ok(allocation);
    }

    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [HttpDelete("allocations/{id:long}")]
    public async Task<IActionResult> UnallocateBed(long id, [FromBody] string? reason, CancellationToken ct)
    {
        await _hostelService.UnallocateBedAsync(TenantId, UserId, id, reason, ct);
        return Ok(new { message = "Bed vacated" });
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("waiting-list")]
    public async Task<IActionResult> AddToWaitingList([FromBody] WaitingListDto req, CancellationToken ct)
    {
        // Simplified: req contains studentId, preferredBlockId, year/term/gender
        var wl = await _hostelService.AddToWaitingListAsync(TenantId, UserId, req.StudentId, req.PreferredBlockId, req.AcademicYearId, req.TermId, req.Gender, ct);
        return Ok(wl);
    }

    // Exeat and leave register
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("exeat")]
    public async Task<IActionResult> CreateExeat([FromBody] CreateExeatRequest req, CancellationToken ct)
    {
        // Would call hostel service CreateExeat
        var exeat = new Entities.ExeatRegister
        {
            TenantId = TenantId,
            StudentId = req.StudentId,
            BlockId = req.BlockId,
            LeaveType = req.LeaveType,
            Reason = req.Reason,
            DepartureDateTime = req.DepartureDateTime,
            ExpectedReturnDateTime = req.ExpectedReturnDateTime,
            Status = "approved",
            AuthorisedByUserId = UserId,
            AuthoriserRole = "house_master",
            ContactPhoneDuringLeave = req.ContactPhone,
            DestinationAddress = req.DestinationAddress,
            AccompanyingPerson = req.AccompanyingPerson,
            CreatedBy = UserId
        };
        _db.Set<Entities.ExeatRegister>().Add(exeat);
        await _db.SaveChangesAsync(ct);
        return Ok(exeat);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("exeat/on-leave")]
    public async Task<IActionResult> GetOnLeave(CancellationToken ct)
    {
        var onLeave = await _db.Set<Entities.ExeatRegister>().Where(e => e.TenantId == TenantId && e.Status == "approved" && e.ActualReturnDateTime == null && !e.IsDeleted).ToListAsync(ct);
        var overdue = onLeave.Where(e => DateTime.UtcNow > e.ExpectedReturnDateTime).ToList();
        return Ok(new { onLeave, overdue, totalOnLeave = onLeave.Count, totalOverdue = overdue.Count });
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("exeat/{id:long}/return")]
    public async Task<IActionResult> ReturnFromLeave(long id, [FromBody] ReturnFromLeaveRequest req, CancellationToken ct)
    {
        var exeat = await _db.Set<Entities.ExeatRegister>().FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted, ct);
        if (exeat == null) return NotFound();
        exeat.ActualReturnDateTime = req.ActualReturnDateTime;
        exeat.Status = "returned";
        await _db.SaveChangesAsync(ct);
        return Ok(exeat);
    }

    // Nightly roll call
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("roll-calls")]
    public async Task<IActionResult> CreateRollCall([FromBody] CreateRollCallRequest req, CancellationToken ct)
    {
        var rollCall = await _hostelService.CreateRollCallAsync(TenantId, UserId, req, ct);
        return Ok(rollCall);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("roll-calls/{id:long}/mark")]
    public async Task<IActionResult> MarkRollCall(long id, [FromBody] MarkRollCallRequest req, CancellationToken ct)
    {
        var result = await _hostelService.MarkRollCallAsync(TenantId, UserId, id, req, ct);
        return Ok(result);
    }

    // Visitor log
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("visitors")]
    public async Task<IActionResult> CreateVisitorLog([FromBody] CreateVisitorLogRequest req, CancellationToken ct)
    {
        var log = new Entities.VisitorLog
        {
            TenantId = TenantId,
            StudentId = req.StudentId,
            BlockId = req.BlockId,
            VisitorName = req.VisitorName,
            Relationship = req.Relationship,
            IdNumber = req.IdNumber,
            Phone = req.Phone,
            CheckInDateTime = DateTime.UtcNow, // recorded by the server at check-in, not client-supplied
            Purpose = req.Purpose,
            AuthorisedByUserId = UserId,
            Status = "checked_in",
            CreatedBy = UserId
        };
        _db.Set<Entities.VisitorLog>().Add(log);
        await _db.SaveChangesAsync(ct);
        return Ok(log);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("visitors/{id:long}/checkout")]
    public async Task<IActionResult> CheckoutVisitor(long id, [FromBody] CheckoutVisitorRequest req, CancellationToken ct)
    {
        var log = await _db.Set<Entities.VisitorLog>().FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId && !v.IsDeleted, ct);
        if (log == null) return NotFound();
        log.CheckOutDateTime = req.CheckOutDateTime;
        log.Status = "checked_out";
        await _db.SaveChangesAsync(ct);
        return Ok(log);
    }

    // Incident and sick bay with access restrictions
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("incidents")]
    [Authorize(Roles = "BOARDING_MASTER,MATRON,HEAD_TEACHER,SCHOOL_ADMIN")]
    public async Task<IActionResult> CreateIncident([FromBody] CreateIncidentRequest req, CancellationToken ct)
    {
        // Access restrictions: only house master, matron, nurse, head, admin can create/view based on visibility
        var incident = new Entities.IncidentRecord
        {
            TenantId = TenantId,
            BlockId = req.BlockId,
            RoomId = req.RoomId,
            StudentId = req.StudentId,
            IncidentType = req.IncidentType,
            Title = req.Title,
            Description = req.Description,
            Severity = req.Severity,
            IncidentDateTime = req.IncidentDateTime,
            ReportedByUserId = UserId,
            ActionTaken = req.ActionTaken,
            Status = "open",
            Visibility = req.Visibility,
            IsConfidential = req.IsConfidential,
            CreatedBy = UserId
        };
        _db.Set<Entities.IncidentRecord>().Add(incident);
        await _db.SaveChangesAsync(ct);
        return Ok(incident);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("incidents")]
    public async Task<IActionResult> GetIncidents([FromQuery] long? blockId, [FromQuery] long? studentId, CancellationToken ct)
    {
        var userRoles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        var query = _db.Set<Entities.IncidentRecord>().Where(i => i.TenantId == TenantId && !i.IsDeleted);

        // Access restrictions: filter by visibility based on user role
        // House master can see house_master, matron can see matron, etc. For demo, if user is not in allowed roles, filter out confidential
        if (!userRoles.Contains("SCHOOL_ADMIN") && !userRoles.Contains("HEAD_TEACHER"))
        {
            // Only show incidents where visibility matches user role or non-confidential
            var allowedVisibilities = new List<string>();
            if (userRoles.Contains("BOARDING_MASTER")) allowedVisibilities.Add("house_master");
            if (userRoles.Contains("MATRON")) { allowedVisibilities.Add("matron"); allowedVisibilities.Add("nurse"); }
            if (userRoles.Contains("NURSE")) allowedVisibilities.Add("nurse");
            if (allowedVisibilities.Any())
                query = query.Where(i => allowedVisibilities.Contains(i.Visibility) || !i.IsConfidential);
            else
                query = query.Where(i => !i.IsConfidential);
        }

        if (blockId.HasValue) query = query.Where(i => i.BlockId == blockId.Value);
        if (studentId.HasValue) query = query.Where(i => i.StudentId == studentId.Value);

        var list = await query.OrderByDescending(i => i.IncidentDateTime).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    // Reports
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/occupancy")]
    public async Task<IActionResult> GetOccupancyReport([FromQuery] long? blockId, CancellationToken ct)
    {
        var report = await _hostelService.GetOccupancyReportAsync(TenantId, blockId, ct);
        return Ok(report);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/vacancies")]
    public async Task<IActionResult> GetVacancyReport(CancellationToken ct)
    {
        var vacantBeds = await _hostelService.GetVacantBedsAsync(TenantId, null, null, ct);
        var byBlock = vacantBeds.GroupBy(b => b.BlockId).ToDictionary(g => g.First().BlockId.ToString(), g => g.Count());
        var byGender = new Dictionary<string, int>(); // would group by block gender
        return Ok(new VacancyReportDto(vacantBeds, vacantBeds.Count, byBlock, byGender));
    }

    // Printable bed allocation list per block and leave register for gate
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("blocks/{blockId:long}/bed-allocation-list/print")]
    public async Task<IActionResult> PrintBedAllocationList(long blockId, CancellationToken ct)
    {
        var block = await _db.Set<Entities.HostelBlock>().FirstOrDefaultAsync(b => b.Id == blockId && b.TenantId == TenantId && !b.IsDeleted, ct);
        if (block == null) return NotFound();
        var allocations = await _db.Set<Entities.BedAllocation>().Where(a => a.TenantId == TenantId && a.BlockId == blockId && a.Status == "active" && !a.IsDeleted).Include(a => a.Bed).ToListAsync(ct);

        var html = $@"
<html><head><style>table{{border-collapse:collapse;width:100%}} th,td{{border:1px solid black;padding:6px;font-size:12px}} @media print{{body{{margin:0}}}}</style></head><body>
<h1>Bed Allocation List - {block.Name} ({block.Code}) - Gender {block.GenderDesignation}</h1>
<p>Generated {DateTime.UtcNow:dd/MM/yyyy HH:mm} | Capacity {block.Capacity}</p>
<table><tr><th>Room</th><th>Bed</th><th>Student</th><th>Student Number</th><th>Grade</th></tr>
{string.Join("", allocations.Select(a => $"<tr><td>{a.RoomId}</td><td>{a.BedId}</td><td>{a.StudentId}</td><td></td><td></td></tr>"))}
</table>
<p>Printed from LearnCloud Hostel Module - Bulawayo</p>
</body></html>";
        return Content(html, "text/html");
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("leave-register/gate/print")]
    public async Task<IActionResult> PrintLeaveRegisterForGate(CancellationToken ct)
    {
        var onLeave = await _db.Set<Entities.ExeatRegister>().Where(e => e.TenantId == TenantId && e.Status == "approved" && e.ActualReturnDateTime == null && !e.IsDeleted).ToListAsync(ct);
        var html = $@"
<html><head><style>table{{border-collapse:collapse;width:100%}} th,td{{border:1px solid black;padding:6px;font-size:12px}}</style></head><body>
<h1>Leave Register for Gate - {DateTime.UtcNow:dd/MM/yyyy}</h1>
<table><tr><th>Student</th><th>Block</th><th>Leave Type</th><th>Reason</th><th>Departure</th><th>Expected Return</th><th>Contact Phone</th><th>Authorised By</th></tr>
{string.Join("", onLeave.Select(e => $"<tr><td>{e.StudentId}</td><td>{e.BlockId}</td><td>{e.LeaveType}</td><td>{e.Reason}</td><td>{e.DepartureDateTime:dd/MM HH:mm}</td><td>{e.ExpectedReturnDateTime:dd/MM HH:mm}</td><td>{e.ContactPhoneDuringLeave}</td><td>{e.AuthorisedByUserId}</td></tr>"))}
</table>
<p>Gate use: check ID, allow departure only if on list and authorised, record actual return time when student returns</p>
</body></html>";
        return Content(html, "text/html");
    }
}
