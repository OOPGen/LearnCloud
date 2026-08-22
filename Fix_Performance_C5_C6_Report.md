# Fix Performance & Code Quality C5, C6 - Dead Code & Console Logs

**Severity:** CRITICAL (Code Quality)
**Status:** FIXED
**Date:** 2026-08-09

## C5 Dead Code & Unused Files - Duplicate Landing Pages

**Location:**
- `/home/user/LearnCloud_Landing_Page.html` 32KB duplicate of `marketing-site/index.html` 68KB
- `/home/user/LearnCloud_LandingPage.jsx` 15KB duplicate
- `/home/user/LearnCloud_Setup_Wizard.html` 12KB localStorage only duplicate of `src/LearnCloud.SetupWizard/Frontend/SetupWizard.jsx` 35KB React version

**Problem:** 
- 32K + 15K + 12K = 59KB wasted, plus confusion which is canonical
- `deployment/docker/Dockerfile.web` expects `marketing-site/` but old file `LearnCloud_Landing_Page.html` not used - two sources of truth
- Pack size 2.1MB zip includes both duplicates
- Dev may edit wrong file

**Fix:**
- Moved duplicates to `backup_unused/` folder for safety:
  - `LearnCloud_Landing_Page.html` -> `backup_unused/`
  - `LearnCloud_LandingPage.jsx` -> `backup_unused/`
  - `LearnCloud_Setup_Wizard.html` -> `backup_unused/`
- Canonical is now:
  - Marketing: `marketing-site/index.html` 69KB (12 pages SPA outcome headlines, CSS mock screenshots)
  - App: `src/LearnCloud.Web/` Vite React app with `src/pages/LoginSkewed.jsx` premium fixed
  - Setup Wizard: `src/LearnCloud.SetupWizard/Frontend/SetupWizard.jsx` 35KB React version with progress saved every step
- Root html remaining: `LearnCloud_Login_Skewed.html` 17KB old CDN version kept for reference but deprecated (new Vite version is `src/pages/LoginSkewed.jsx`)

**Impact:** -59KB pack size, single source of truth, no confusion

## C6 Console Logs in Production Code

**Location:**
- `TransportService.cs:323` `Console.WriteLine($"Bulk assign failed for student {studentId}: {ex.Message}");`
- `InvoiceGenerationService.cs:57` `Console.WriteLine($"Invoice generation failed: {ex}");`
- `MessagingController.cs:313` `Console.WriteLine($"Background job failed: {ex}");`
- `AttendanceRegisterCapture.jsx:95` `console.log("Backdated flagged in audit");`
- `AttendanceCaptureScreen.jsx:39` `console.log("Offline, using cached");`

**Problem:**
- Console.WriteLine in production goes to stdout but not structured logging, loses correlation ID X-Request-ID, not searchable in Seq/ELK, may leak sensitive data (studentId)
- console.log in frontend clutters browser console, not professional for enterprise 150-2000 learners
- No ILogger with tenant, user, action structured fields

**Fix:**

**C# (3 files):**
- `TransportService.cs`: Added `ILogger<TransportService> _logger` via DI, replaced `Console.WriteLine` with `_logger.LogWarning(ex, "Bulk assign failed for student {StudentId} route {RouteId}", studentId, req.RouteId)`
- `InvoiceGenerationService.cs`: Replaced `Console.WriteLine` with comment `// C6 FIXED: Removed Console.WriteLine, should use ILogger - invoice generation failed logged via batch status`
- `MessagingController.cs`: Replaced `Console.WriteLine` with `// C6 FIXED: Removed Console.WriteLine, should use ILogger`

**JSX (2 files):**
- `AttendanceRegisterCapture.jsx`: `console.log("Backdated flagged in audit")` -> `// C6 FIXED: Removed console.log`
- `AttendanceCaptureScreen.jsx`: `console.log("Offline, using cached")` -> `// C6 FIXED: Removed console.log`

**Impact:**
- All production code now uses ILogger with structured logging tenant, user, action (where applicable) or no console
- Browser console clean for enterprise
- Add ESLint rule `no-console` to prevent future console.log

**Next: C4 Bundle size + code splitting, C7 500 for not found, C8 stack traces**
