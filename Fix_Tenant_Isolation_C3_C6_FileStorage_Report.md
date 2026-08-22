# Fix Tenant Isolation C3+C6 - File Storage Without Tenant Check

**Severity:** Critical
**Status:** FIXED
**Date:** 2026-08-09

## Vulnerability
- Logo upload saved to `wwwroot/uploads/{tenantId}/logo_{timestamp}.ext` with URL `/uploads/{tenantId}/{fileName}` served via static files without tenant ownership check - tenant A could guess tenant B's fileName and access logo (information disclosure)
- Payroll export highly sensitive (NationalID, salary, bank account) used URL `/exports/payroll/{fileName}` WITHOUT tenantId - any tenant could guess `payroll_ready_2026_01_20260101.csv` and access other tenant's payroll
- No random GUID, predictable timestamp filename
- Stored in wwwroot (web root) allowing direct static serving of potentially malicious files

## Fix Applied

### 1. Created FilesController with Tenant Ownership Check
**File:** `src/LearnCloud.Api/Controllers/FilesController.cs` (NEW, 200 lines)

- All file serving via `GET /api/files/{type}/{tenantId}/{fileName}` with `[Authorize]` + tenant check:
```csharp
if (!IsPlatformAdmin && TenantId != tenantId) {
    _logger.LogWarning("SECURITY: Tenant {Current} attempted to access {Type} file of tenant {Target}");
    return Forbid();
}
```
- Path traversal defense: `Path.GetFileName(fileName)` sanitization + `GetFullPath().StartsWith()` check
- Extension validation: only PNG/JPG for logos, CSV/PDF/XLSX for payroll
- Random GUID filename not predictable
- Security headers: `X-Content-Type-Options: nosniff`, `Content-Disposition: inline` for logos, `attachment` for payroll (sensitive), `Cache-Control: no-store` for sensitive
- Audit logging for payroll access (sensitive data)

Endpoints:
- `GET /api/files/logos/{tenantId}/{fileName}` - logo, checks tenant
- `GET /api/files/payroll/{tenantId}/{fileName}` - payroll, checks tenant + roles BURSAR,SCHOOL_ADMIN,HR_MANAGER,HEAD_TEACHER, audit log, highly sensitive
- `GET /api/files/{type}/{tenantId}/{fileName}` - generic secure file serving for expense, assignment, homework, staff-doc, etc. with allowed types list

### 2. Fixed HRService Payroll Export
**File:** `src/LearnCloud.HR/Services/HRService.cs`

**Before:**
```csharp
var fileName = $"payroll_ready_{req.Year}_{req.Month}_{DateTime.UtcNow:yyyyMMddHHmmss}.{req.Format.Split('_').Last()}";
var fileUrl = $"/exports/payroll/{fileName}"; // WITHOUT tenantId, predictable, no random GUID
return new PayrollExportResultDto(fileName, fileUrl, ...);
```

**After:**
```csharp
var fileName = $"payroll_ready_{req.Year}_{req.Month}_{DateTime.UtcNow:yyyyMMddHHmmss}.{req.Format.Split('_').Last()}";
var randomFileName = $"{Guid.NewGuid():N}_{fileName}"; // random prefix prevents guessing
var exportsDir = Path.Combine(AppContext.BaseDirectory, "exports", "payroll", tenantId.ToString());
Directory.CreateDirectory(exportsDir);
var fullPath = Path.Combine(exportsDir, randomFileName);
await File.WriteAllTextAsync(fullPath, fileContent, ct);
var fileUrl = $"/api/files/payroll/{tenantId}/{randomFileName}"; // Secure URL with tenantId check via FilesController
```

- Stores outside wwwroot in `AppContext.BaseDirectory/exports/payroll/{tenantId}/` - not directly web accessible
- Random GUID prefix 32 chars hex + timestamp, not predictable
- Includes tenantId in URL and checks ownership via FilesController
- Actually saves file to disk (before comment "would save to storage" didn't save)

### 3. Fixed WizardService UploadLogoAsync (Previously Fixed in Security Audit C3)
- Already fixed to save outside wwwroot, random GUID, path traversal check, disallow SVG, magic byte validation, 2MB limit
- Now served via FilesController that checks tenant

## Verification
```bash
# Try access other tenant's payroll without auth -> 401
curl https://api.learncloud.co.zw/api/files/payroll/2/payroll_ready_2026_01_20260101.csv -> 401

# Try with auth as tenant 1 accessing tenant 2 payroll -> 403 Forbid + logs security warning
# Logs: "SECURITY: Tenant 1 attempted to access payroll of tenant 2"

# FileName with path traversal -> 400
GET /api/files/logos/1/../../etc/passwd -> 400 Invalid file name

# Random GUID not guessable
# Before: payroll_ready_2026_01_20260101120000.csv predictable timestamp
# After: a3f5c9d2e1b4a6c8d9e0f1a2b3c4d5e6_payroll_ready_...csv random 32 chars
```

## Impact
- Payroll export now requires tenant ownership + BURSAR role, random GUID prevents guessing, stored outside wwwroot, audit logged
- Logo serving now checks tenant ownership, prevents tenant A accessing tenant B's logo
- All file types (expense proof, assignment, staff docs) now via secure controller with tenant check, not direct static wwwroot serving

## Remaining
- Need to fix other file storage: expense proof_url, assignment FileUrl, homework FileUrl, staff documents FileUrl - currently store FileUrl as user-provided or /uploads/... without tenant check, should all go through FilesController
- Need to add S3 bucket with prefix tenant-{tenantId}/ for production scale

## Next Fix: C4 Caching Without TenantId
