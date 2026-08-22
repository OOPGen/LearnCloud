using LearnCloud.HR.DTOs;
using LearnCloud.HR.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.HR.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class HRController : ControllerBase
{
    private readonly IHRService _hrService;
    private readonly ITenantContext _tenantContext;

    public HRController(IHRService hrService, ITenantContext tenantContext)
    {
        _hrService = hrService;
        _tenantContext = tenantContext;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("staff")]
    [Authorize(Roles = "HR_MANAGER,SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest req, CancellationToken ct)
    {
        var staff = await _hrService.CreateStaffAsync(TenantId, UserId, req, ct);
        return Ok(staff);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("staff")]
    public async Task<IActionResult> GetStaff(CancellationToken ct)
    {
        var list = await _hrService.GetStaffAsync(TenantId, ct);
        return Ok(list);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("contracts")]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractRequest req, CancellationToken ct)
    {
        var contract = await _hrService.CreateContractAsync(TenantId, UserId, req, ct);
        return Ok(contract);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("contracts/expiring")]
    public async Task<IActionResult> GetExpiringContracts([FromQuery] int daysAhead = 30, CancellationToken ct = default)
    {
        var contracts = await _hrService.GetExpiringContractsAsync(TenantId, daysAhead, ct);
        return Ok(contracts);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("leave-requests")]
    public async Task<IActionResult> CreateLeaveRequest([FromBody] CreateLeaveRequestDto req, CancellationToken ct)
    {
        var leave = await _hrService.CreateLeaveRequestAsync(TenantId, UserId, req, ct);
        return Ok(leave);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("leave-requests/{id:long}/approve")]
    [Authorize(Roles = "HR_MANAGER,HEAD_TEACHER,SCHOOL_ADMIN,DIRECTOR")]
    public async Task<IActionResult> ApproveLeave(long id, [FromBody] ApproveLeaveRequest req, CancellationToken ct)
    {
        var leave = await _hrService.ApproveLeaveAsync(TenantId, UserId, id, req, ct);
        return Ok(leave);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("leave-balances/{staffId:long}")]
    public async Task<IActionResult> GetLeaveBalance(long staffId, [FromQuery] int academicYear = 2026, CancellationToken ct = default)
    {
        var balance = await _hrService.GetLeaveBalanceAsync(TenantId, staffId, academicYear, ct);
        return Ok(balance);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("leave-calendar")]
    public async Task<IActionResult> GetLeaveCalendar([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var calendar = await _hrService.GetLeaveCalendarAsync(TenantId, from, to, ct);
        return Ok(calendar);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("appraisal-cycles")]
    public async Task<IActionResult> CreateAppraisalCycle([FromBody] CreateAppraisalCycleRequest req, CancellationToken ct)
    {
        var cycle = await _hrService.CreateAppraisalCycleAsync(TenantId, UserId, req, ct);
        return Ok(cycle);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("disciplinary")]
    [Authorize(Roles = "HR_MANAGER,HEAD_TEACHER,SCHOOL_ADMIN,DIRECTOR")]
    public async Task<IActionResult> CreateDisciplinary([FromBody] CreateDisciplinaryRequest req, CancellationToken ct)
    {
        var record = await _hrService.CreateDisciplinaryAsync(TenantId, UserId, req, ct);
        return Ok(record);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/headcount")]
    public async Task<IActionResult> GetHeadcountReport(CancellationToken ct)
    {
        var report = await _hrService.GetHeadcountReportAsync(TenantId, ct);
        return Ok(report);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("payroll-export")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN,HR_MANAGER")]
    public async Task<IActionResult> ExportPayrollReady([FromBody] PayrollExportRequest req, CancellationToken ct)
    {
        var result = await _hrService.ExportPayrollReadyAsync(TenantId, UserId, req, ct);
        return Ok(result);
    }
}