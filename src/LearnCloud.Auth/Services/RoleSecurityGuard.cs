using LearnCloud.Auth.Entities;

namespace LearnCloud.Auth.Services;

// SECURITY H7 FIX: Prevent privilege escalation via role creation
public static class RoleSecurityGuard
{
    private static readonly string[] PlatformRolePrefixes = { "PLATFORM_", "TENANTS_", "SYSTEM" };
    private static readonly string[] PlatformPermissionPrefixes = { "platform.", "tenants." };

    public static void ValidateRoleCreation(string roleCode, long? tenantId)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
            throw new InvalidOperationException("Role code required");

        // Platform roles must have tenantId null
        bool isPlatformRole = PlatformRolePrefixes.Any(p => roleCode.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        
        if (isPlatformRole && tenantId != null)
        {
            throw new UnauthorizedAccessException($"SECURITY: Tenant {tenantId} cannot create platform role {roleCode} - privilege escalation attempted");
        }

        // Tenant roles cannot be named like platform roles
        if (tenantId != null && isPlatformRole)
        {
            throw new UnauthorizedAccessException($"SECURITY: Role code {roleCode} is reserved for platform. Tenant cannot create platform-prefixed roles.");
        }

        // Platform roles should only be created with null tenantId
        if (tenantId == null && !isPlatformRole && !roleCode.StartsWith("SCHOOL_") && !roleCode.StartsWith("TEACHER") && !roleCode.StartsWith("PARENT"))
        {
            // Allow custom tenant roles? For platform creating tenant roles, need explicit?
            // For now allow, but log
        }
    }

    public static void ValidatePermissionAssignment(string permissionCode, long? tenantId)
    {
        bool isPlatformPerm = PlatformPermissionPrefixes.Any(p => permissionCode.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (isPlatformPerm && tenantId != null)
        {
            throw new UnauthorizedAccessException($"SECURITY: Tenant {tenantId} cannot assign platform permission {permissionCode} - privilege escalation");
        }
    }

    public static void ValidateRoleCodesForTenant(IEnumerable<string> roleCodes, long? tenantId)
    {
        foreach (var code in roleCodes)
        {
            ValidateRoleCreation(code, tenantId);
        }
    }
}
