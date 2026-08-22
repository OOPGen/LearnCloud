using LearnCloud.Core.DTOs;
using LearnCloud.Core.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.Core.Controllers;

[ApiController]
[Route("api/academic/subjects")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _subjectService;
    private readonly ITenantContext _tenantContext;

    public SubjectsController(ISubjectService subjectService, ITenantContext tenantContext)
    {
        _subjectService = subjectService;
        _tenantContext = tenantContext;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant context");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Every list: server-side search, filter, sort, pagination, and CSV export
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,DEPUTY_HEAD,TEACHER,REGISTRAR")]
    public async Task<IActionResult> GetList([FromQuery] SubjectListRequest req, CancellationToken ct)
    {
        var result = await _subjectService.GetListAsync(TenantId, req, ct);
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("{id:long}")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,DEPUTY_HEAD,TEACHER")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var subject = await _subjectService.GetByIdAsync(TenantId, id, ct);
        return Ok(subject);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> Create([FromBody] CreateSubjectRequest req, CancellationToken ct)
    {
        var subject = await _subjectService.CreateAsync(TenantId, UserId, req, ct);
        return CreatedAtAction(nameof(GetById), new { id = subject.Id }, subject);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("{id:long}")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSubjectRequest req, CancellationToken ct)
    {
        var subject = await _subjectService.UpdateAsync(TenantId, UserId, id, req, ct);
        return Ok(subject);
    }

    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [HttpDelete("{id:long}")]
    [Authorize(Roles = "SCHOOL_ADMIN")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _subjectService.DeleteAsync(TenantId, UserId, id, ct);
        return NoContent();
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("assign-to-grade")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> AssignToGrade([FromBody] AssignSubjectToGradeRequest req, CancellationToken ct)
    {
        var result = await _subjectService.AssignToGradeAsync(TenantId, UserId, req, ct);
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("by-grade/{gradeId:long}")]
    public async Task<IActionResult> GetByGrade(long gradeId, [FromQuery] long academicYearId, CancellationToken ct)
    {
        var list = await _subjectService.GetByGradeAsync(TenantId, gradeId, academicYearId, ct);
        return Ok(list);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("export")]
    [Authorize(Roles = "SCHOOL_ADMIN,HEAD_TEACHER,REGISTRAR")]
    public async Task<IActionResult> ExportCsv([FromQuery] SubjectListRequest req, CancellationToken ct)
    {
        var csvBytes = await _subjectService.ExportCsvAsync(TenantId, req, ct);
        return File(csvBytes, "text/csv", $"subjects_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}