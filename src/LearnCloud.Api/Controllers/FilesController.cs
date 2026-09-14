using Microsoft.AspNetCore.RateLimiting;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.Api.Controllers;

// SECURITY FIX C3+C6: File storage with tenant isolation
// - All files stored outside wwwroot in AppContext.BaseDirectory/uploads/{tenantId}/{guid}
// - Served via this controller with tenant ownership check
// - Random GUID filename, not predictable
// - Checks TenantContext.TenantId == path tenantId or platform admin with explicit no-tenant

[ApiController]
[Route("api/files")]
[Authorize]
[EnableRateLimiting("api_general")]
public class FilesController : ControllerBase
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<FilesController> _logger;

    public FilesController(ITenantContext tenantContext, ILogger<FilesController> logger)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant context");
    private bool IsPlatformAdmin => User.IsInRole("PLATFORM_SUPERADMIN");

    // GET /api/files/logos/{tenantId}/{fileName} - serve logo with tenant check
    [HttpGet("logos/{tenantId:long}/{fileName}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLogo(long tenantId, string fileName, CancellationToken ct)
    {
        // SECURITY: Check tenant ownership
        if (!IsPlatformAdmin && TenantId != tenantId)
        {
            _logger.LogWarning("SECURITY: Tenant {CurrentTenant} attempted to access logo of tenant {TargetTenant} file {FileName}", TenantId, tenantId, fileName);
            return Forbid();
        }

        // Sanitize fileName - prevent path traversal
        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName != fileName)
        {
            return BadRequest(new { message = "Invalid file name - path traversal detected" });
        }

        // Only allow png/jpg (no svg per C3 fix)
        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
        {
            return BadRequest(new { message = "Only PNG/JPG allowed" });
        }

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", tenantId.ToString());
        var fullPath = Path.Combine(uploadsDir, safeFileName);
        var fullPathResolved = Path.GetFullPath(fullPath);
        var uploadsDirResolved = Path.GetFullPath(uploadsDir);
        if (!fullPathResolved.StartsWith(uploadsDirResolved, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("SECURITY: Path traversal attempt tenant {Tenant} file {FileName}", tenantId, fileName);
            return BadRequest(new { message = "Invalid file path" });
        }

        if (!System.IO.File.Exists(fullPathResolved))
        {
            return NotFound(new { message = "File not found" });
        }

        var contentType = ext == ".png" ? "image/png" : "image/jpeg";
        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPathResolved, ct);
        
        // Security headers
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeFileName}\"";
        Response.Headers["Cache-Control"] = "public, max-age=3600";
        
        return File(fileBytes, contentType);
    }

    // GET /api/files/payroll/{tenantId}/{fileName} - highly sensitive payroll export
    [HttpGet("payroll/{tenantId:long}/{fileName}")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN,HR_MANAGER,HEAD_TEACHER")]
    public async Task<IActionResult> GetPayrollExport(long tenantId, string fileName, CancellationToken ct)
    {
        // SECURITY: Payroll contains NationalID, salary, bank account - highly sensitive, check tenant + role
        if (!IsPlatformAdmin && TenantId != tenantId)
        {
            _logger.LogCritical("SECURITY: Tenant {Current} attempted to access payroll of tenant {Target} file {File} - payroll contains NationalID, bank account!", TenantId, tenantId, fileName);
            return Forbid();
        }

        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName != fileName)
            return BadRequest(new { message = "Invalid file name" });

        // Only allow csv, pdf, etc. for payroll
        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        var allowed = new[] { ".csv", ".pdf", ".xlsx" };
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Invalid payroll file type" });

        // Payroll files stored in AppContext.BaseDirectory/exports/payroll/{tenantId}/
        var exportsDir = Path.Combine(AppContext.BaseDirectory, "exports", "payroll", tenantId.ToString());
        var fullPath = Path.Combine(exportsDir, safeFileName);
        var fullPathResolved = Path.GetFullPath(fullPath);
        var exportsDirResolved = Path.GetFullPath(exportsDir);
        if (!fullPathResolved.StartsWith(exportsDirResolved, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid file path" });

        if (!System.IO.File.Exists(fullPathResolved))
            return NotFound();

        var contentType = ext switch
        {
            ".csv" => "text/csv",
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPathResolved, ct);
        
        // Security headers for sensitive payroll
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeFileName}\""; // attachment not inline for sensitive
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        
        // Audit log for payroll access (sensitive data)
        _logger.LogInformation("Payroll export accessed tenant {Tenant} file {File} by user {User} IP {Ip}", tenantId, fileName, User.FindFirst("uid")?.Value, HttpContext.Connection.RemoteIpAddress);

        return File(fileBytes, contentType, safeFileName);
    }

    // Generic secure file serving for other types: expense proof, assignment, staff docs, etc.
    [HttpGet("{type}/{tenantId:long}/{fileName}")]
    [Authorize]
    public async Task<IActionResult> GetFile(string type, long tenantId, string fileName, CancellationToken ct)
    {
        // Allowed types
        var allowedTypes = new[] { "expense", "assignment", "homework", "staff-doc", "student-doc", "invoice", "receipt" };
        if (!allowedTypes.Contains(type.ToLower()))
            return BadRequest(new { message = $"Invalid file type {type}. Allowed: {string.Join(", ", allowedTypes)}" });

        if (!IsPlatformAdmin && TenantId != tenantId)
        {
            _logger.LogWarning("SECURITY: Tenant {Current} attempted to access {Type} file of tenant {Target} file {File}", TenantId, type, tenantId, fileName);
            return Forbid();
        }

        var safeFileName = Path.GetFileName(fileName);
        if (safeFileName != fileName)
            return BadRequest(new { message = "Invalid file name" });

        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", type, tenantId.ToString());
        var fullPath = Path.Combine(uploadsDir, safeFileName);
        var fullPathResolved = Path.GetFullPath(fullPath);
        var uploadsDirResolved = Path.GetFullPath(uploadsDir);
        if (!fullPathResolved.StartsWith(uploadsDirResolved, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid path" });

        if (!System.IO.File.Exists(fullPathResolved))
            return NotFound();

        // Determine content type by extension
        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };

        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPathResolved, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeFileName}\"";
        
        return File(fileBytes, contentType);
    }
}
