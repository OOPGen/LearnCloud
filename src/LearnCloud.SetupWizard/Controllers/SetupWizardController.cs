using LearnCloud.MultiTenancy.Context;
using LearnCloud.SetupWizard.DTOs;
using LearnCloud.SetupWizard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.SetupWizard.Controllers;

[ApiController]
[Route("api/setup")]
[Authorize] // All wizard steps require authenticated tenant admin, except defaults which allow anonymous for pre-login? For V1 we require auth
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class SetupWizardController : ControllerBase
{
    private readonly IWizardService _wizard;
    private readonly ITenantContext _tenantContext;

    public SetupWizardController(IWizardService wizard, ITenantContext tenantContext)
    {
        _wizard = wizard;
        _tenantContext = tenantContext;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant context");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // GET /api/setup/progress - resume
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress(CancellationToken ct)
    {
        var progress = await _wizard.GetProgressAsync(TenantId, ct);
        return Ok(progress);
    }

    // GET /api/setup/data - full data for resume
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("data")]
    public async Task<IActionResult> GetFullData(CancellationToken ct)
    {
        var data = await _wizard.GetFullDataAsync(TenantId, ct);
        return Ok(data);
    }

    // PUT /api/setup/step/1 - save step, creates domain entities, progress saved after every step
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("step/{step:int}")]
    public async Task<IActionResult> SaveStep(int step, [FromBody] System.Text.Json.JsonElement body, CancellationToken ct)
    {
        try
        {
            var result = await _wizard.SaveStepAsync(TenantId, UserId, step, body, ct);
            return Ok(result);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { message = "Validation failed", errors = ex.Errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "An error occurred processing your request", requestId = HttpContext.TraceIdentifier, code = "BAD_REQUEST" }); // C7/C8 FIX: Was ex.Message exposing internal details
        }
    }

    // POST /api/setup/skip/4 - skip except 1 and 3
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("skip/{step:int}")]
    public async Task<IActionResult> SkipStep(int step, CancellationToken ct)
    {
        try
        {
            var result = await _wizard.SkipStepAsync(TenantId, UserId, step, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "An error occurred processing your request", requestId = HttpContext.TraceIdentifier, code = "BAD_REQUEST" }); // C7/C8 FIX: Was ex.Message exposing internal details
        }
    }

    // GET /api/setup/summary - completion summary counts
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _wizard.GetSummaryAsync(TenantId, ct);
        return Ok(summary);
    }

    // POST /api/setup/complete - mark tenant setup as complete and route to dashboard with next 3 actions
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        try
        {
            var summary = await _wizard.CompleteAsync(TenantId, UserId, ct);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "An error occurred processing your request", requestId = HttpContext.TraceIdentifier, code = "BAD_REQUEST" }); // C7/C8 FIX: Was ex.Message exposing internal details
        }
    }

    // POST /api/setup/branding/logo - logo upload - SECURITY C3 FIX: No SVG, magic byte validation
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("branding/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)] // SECURITY: 2MB max for logo (was 5MB) - prevents large payload DoS
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "No file" });
        if (file.Length > 2 * 1024 * 1024) return BadRequest(new { message = "Max 2MB for logo - use compressed PNG/JPG" });
        
        // SECURITY C3: Disallow SVG for V1 to prevent Stored XSS via <script> in SVG
        // If SVG required later, use SvgSanitizer library and serve with Content-Disposition
        var allowed = new[] { ".png", ".jpg", ".jpeg" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        // Sanitize fileName - prevent path traversal, use only extension
        var safeFileName = Path.GetFileName(file.FileName);
        if (!allowed.Contains(ext)) return BadRequest(new { message = "Only PNG/JPG allowed - SVG disabled for security (Stored XSS). Use PNG with transparency." });

        // Magic byte validation - prevent extension spoofing (e.g., PHP file renamed to .png)
        using var streamForCheck = file.OpenReadStream();
        var header = new byte[8];
        await streamForCheck.ReadAsync(header, 0, 8, ct);
        streamForCheck.Position = 0;
        
        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        bool isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        // SVG would start with <svg or <?xml but we already disallow
        
        if (!isPng && !isJpg)
        {
            return BadRequest(new { message = "Invalid file content - must be real PNG/JPG (magic byte check failed). Possible file type spoofing." });
        }

        // Additional check: ContentType from client is untrusted, but log mismatch
        var ctLower = file.ContentType?.ToLowerInvariant() ?? "";
        if (!ctLower.Contains("image/png") && !ctLower.Contains("image/jpeg") && !ctLower.Contains("image/jpg"))
        {
            // Allow if magic bytes pass, but warn - client ContentType spoofed
            // For strict, we could reject, but magic byte is source of truth
        }

        using var stream = file.OpenReadStream();
        var result = await _wizard.UploadLogoAsync(TenantId, UserId, stream, safeFileName, ct);
        return Ok(result);
    }

    // GET /api/setup/defaults/subjects?schoolType=secondary
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("defaults/subjects")]
    [AllowAnonymous] // allow before auth for preview
    public async Task<IActionResult> GetDefaultSubjects([FromQuery] string schoolType = "secondary", CancellationToken ct = default)
    {
        // If tenant context missing (anonymous), use 0 but service doesn't need tenant for defaults
        var tenantId = _tenantContext.TenantId ?? 0;
        var subjects = await _wizard.GetDefaultSubjectsAsync(tenantId, schoolType, ct);
        return Ok(subjects);
    }

    // GET /api/setup/defaults/grading?preference=secondary
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("defaults/grading")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDefaultGrading([FromQuery] string? preference = null, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? 0;
        var bands = await _wizard.GetDefaultGradingAsync(tenantId, preference, ct);
        return Ok(bands);
    }

    // GET /api/setup/defaults/departments
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("defaults/departments")]
    [AllowAnonymous]
    public IActionResult GetDefaultDepartments()
    {
        return Ok(new
        {
            departments = Seed.DepartmentDefaults.DefaultDepartments,
            roles = Seed.DepartmentDefaults.DefaultCustomRoles
        });
    }

    // GET /api/setup/defaults/academic - sensible defaults everywhere
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("defaults/academic")]
    [AllowAnonymous]
    public IActionResult GetAcademicDefaults()
    {
        var (start, end) = Seed.AcademicDefaults.GetCurrentAcademicYear();
        var terms = Seed.AcademicDefaults.GetThreeTerms(start, end);
        return Ok(new
        {
            academicYear = new { name = start.Year.ToString(), startDate = start, endDate = end, isCurrent = true },
            terms = new { count = 3, terms = terms.Select(t => new { name = t.name, termNumber = t.number, startDate = t.start, endDate = t.end, isCurrent = t.number == 1 }) },
            timezone = "Africa/Harare",
            baseCurrency = "USD",
            zwgEnabled = true,
            primaryColor = "#0F153A",
            secondaryColor = "#5F3F96",
            weekStart = "Monday",
            attendanceMode = "daily",
            invoicePrefix = "INV",
            invoiceNextNumber = 1,
            invoiceFormat = "{prefix}-{year}-{number:5}"
        });
    }
}