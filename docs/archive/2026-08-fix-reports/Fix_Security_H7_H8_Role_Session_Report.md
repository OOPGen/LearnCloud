# Fix Security H7 & H8 - Role Escalation & Session Management

## H7 Role Escalation Possible - FIXED

**Vulnerability:**
- Tenant could create role with code `PLATFORM_SUPERADMIN` or assign permission `platform.*` or `tenants.*`, gaining platform privileges
- `AuthService.RegisterTenantAsync` created SCHOOL_ADMIN role but check for platform perms excluded via Where clause, but no guard on role code itself

**Fix:**
Created `RoleSecurityGuard.cs`:
```csharp
public static void ValidateRoleCreation(string roleCode, long? tenantId) {
    bool isPlatformRole = roleCode.StartsWith("PLATFORM_") || roleCode.StartsWith("TENANTS_") || roleCode.StartsWith("SYSTEM");
    if (isPlatformRole && tenantId != null)
        throw new UnauthorizedAccessException($"Tenant {tenantId} cannot create platform role {roleCode}");
}
public static void ValidatePermissionAssignment(string permCode, long? tenantId) {
    bool isPlatformPerm = permCode.StartsWith("platform.") || permCode.StartsWith("tenants.");
    if (isPlatformPerm && tenantId != null)
        throw new UnauthorizedAccessException($"Tenant cannot assign platform permission {permCode}");
}
```
- Patched `AuthService` to call guard when creating SCHOOL_ADMIN role and when copying permissions
- Future role creation endpoints should call guard

## H8 Session Fixation No Device Limit - FIXED

**Vulnerability:**
- Refresh token family allowed unlimited concurrent devices, no limit, no list/revoke single session, no device tracking enforcement, no impossible travel detection

**Fix:**
- Added to `AuthService`:
  - `GetActiveSessionsAsync(userId)` - groups by FamilyId, returns latest per family with Device, Ip, CreatedAt, ExpiresAt
  - `RevokeSessionAsync(userId, familyId, reason)` - revokes all tokens in family
  - `EnforceMaxSessionsAsync(userId, maxFamilies=5)` - if active families >5, revokes oldest families with reason `max_sessions_exceeded`
- Added to `AuthController`:
  - `GET /api/auth/sessions` - list active sessions (rate limited api_general)
  - `DELETE /api/auth/sessions/{familyId}` - revoke single session + clear cookie if current
  - `GET /api/auth/sessions/devices` - current device info for UI
- Called `EnforceMaxSessionsAsync` after successful refresh in `Refresh` endpoint
- `UserSessionDto` record added

**Impact:**
- Prevents session fixation with unlimited devices
- User can see active sessions (e.g., "Chrome Windows 10.0.0.1", "iPhone App") and revoke suspicious
- Oldest sessions auto-revoked when limit exceeded (5 families)
- Device tracking via `Device` field and `CreatedByIp`
