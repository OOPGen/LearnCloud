namespace LearnCloud.MultiTenancy.Security;

// The single list of roles allowed to run code outside a tenant. The tenant context,
// the database context and NoTenantOperation each used to keep their own list, and the
// lists disagreed: "SYSTEM_JOB" passed one check and failed another, so the dunning and
// messaging jobs could never start their no-tenant scope.
public static class PrivilegedRoles
{
    public const string PlatformSuperAdmin = "PLATFORM_SUPERADMIN";
    public const string PlatformSupport = "PLATFORM_SUPPORT";
    public const string SystemJob = "SYSTEM_JOB";
    public const string Migration = "MIGRATION";

    private static readonly HashSet<string> NoTenantRoles = new(StringComparer.Ordinal)
    {
        PlatformSuperAdmin, PlatformSupport, SystemJob, Migration
    };

    public static IReadOnlyCollection<string> All => NoTenantRoles;

    public static bool CanUseNoTenantScope(string? role) => role is not null && NoTenantRoles.Contains(role);
}
