# Fix Security C3 - Stored XSS via SVG Logo Upload

**Severity:** Critical
**Status:** FIXED
**Date:** 2026-08-09

## Vulnerability
- Endpoint `POST /api/setup/branding/logo` allowed `.svg` extension
- Saved file directly to `wwwroot/uploads/{tenantId}/logo_{timestamp}{ext}` via static file serving
- SVG can contain `<script>alert(localStorage.getItem('access_token'))</script>` stealing JWT from localStorage (C2 chain)
- No magic byte validation, only extension check via `Path.GetExtension(file.FileName)` - spoofable
- No path traversal check for final filename (though newName used timestamp, still extension from user input)
- Served via wwwroot static middleware - allows execution as image with script

**Impact:** Stored XSS - every user, parent, teacher viewing branding sees malicious logo, token exfiltration, defaced report cards/invoices.

## Fix Applied

### Controller `SetupWizardController.cs`
```csharp
// BEFORE:
var allowed = new[] { ".png", ".jpg", ".jpeg", ".svg" };
var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowed.Contains(ext)) return BadRequest("Only PNG/JPG/SVG allowed");
using var stream = file.OpenReadStream();
var result = await _wizard.UploadLogoAsync(..., file.FileName, ...);

// AFTER (SECURE):
[RequestSizeLimit(2 * 1024 * 1024)] // reduced from 5MB to 2MB
var allowed = new[] { ".png", ".jpg", ".jpeg" }; // SVG REMOVED
var safeFileName = Path.GetFileName(file.FileName); // sanitize
var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowed.Contains(ext)) return BadRequest("Only PNG/JPG allowed - SVG disabled for security");

// Magic byte validation
using var streamForCheck = file.OpenReadStream();
var header = new byte[8];
await streamForCheck.ReadAsync(header, 0, 8, ct);
bool isPng = header[0]==0x89 && header[1]==0x50 && header[2]==0x4E && header[3]==0x47;
bool isJpg = header[0]==0xFF && header[1]==0xD8 && header[2]==0xFF;
if (!isPng && !isJpg) return BadRequest("Invalid file content - magic byte check failed");
```

### Service `WizardService.cs`
```csharp
// BEFORE:
var uploadsDir = Path.Combine("wwwroot","uploads",tenantId.ToString());
var newName = $"logo_{timestamp}{ext}";
var path = Path.Combine(uploadsDir,newName);
var url = $"/uploads/{tenantId}/{newName}";

// AFTER:
var ext = Path.GetExtension(fileName).ToLowerInvariant();
if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") ext = ".png"; // force safe
var randomName = $"{Guid.NewGuid():N}{ext}"; // random GUID, not guessable timestamp
var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", tenantId.ToString()); // outside wwwroot
Directory.CreateDirectory(uploadsDir);
var fullPath = Path.Combine(uploadsDir, randomName);
// Path traversal defense
var fullPathResolved = Path.GetFullPath(fullPath);
var uploadsDirResolved = Path.GetFullPath(uploadsDir);
if (!fullPathResolved.StartsWith(uploadsDirResolved, OrdinalIgnoreCase))
    throw new InvalidOperationException("Invalid file path - potential traversal");
using var fs = new FileStream(fullPathResolved, FileMode.CreateNew, ...);
var url = $"/api/files/logos/{tenantId}/{randomName}"; // secure endpoint, not direct static
```

**Additional Hardening:**
- File size check after write prevents zip bomb
- Random GUID filename prevents enumeration
- Stored outside wwwroot prevents direct static serving of malicious content
- URL now via `/api/files/logos/` controller which should serve with `Content-Disposition: inline; filename=`, `Content-Type: image/png`, `X-Content-Type-Options: nosniff`, and `Content-Security-Policy: default-src 'none';`
- If SVG required later, use `SvgSanitizer` library removing `<script>`, `onload`, `javascript:` hrefs, and serve with `Content-Security-Policy`

## Verification
```bash
grep -Rni "\.svg" src/LearnCloud.SetupWizard --include="*.cs"
# Only in comments about disabled, no longer in allowed list
```

## Remaining for Full Chain Fix
- Need to implement `/api/files/logos/{tenantId}/{fileName}` endpoint with secure headers (planned in C2 fix)
- Add CSP header globally (H2)
- Fix C2 localStorage token storage (next critical)

## Next
**C2 - access_token in localStorage XSS** is next - requires creating centralized `apiClient.js` replacing 100+ `localStorage.getItem('access_token')` usages with HttpOnly cookie + memory storage, adding CSP.
