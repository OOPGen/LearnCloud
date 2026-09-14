using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Auth.Entities;

public class Role : BaseEntity
{
    // Null for system/platform roles
    public long? TenantId { get; set; }
    public string Code { get; set; } = null!; // SCHOOL_ADMIN etc
    public string Name { get; set; } = null!;
    public bool IsSystem { get; set; } = true;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Permission : BaseEntity
{
    public long? TenantId { get; set; } // global perms tenantId null, but column present for uniformity
    public string Code { get; set; } = null!; // e.g. students.read
    public string Name { get; set; } = null!;
    public string Module { get; set; } = null!;
}

public class RolePermission : BaseEntity
{
    // Overrides: RolePermission must be tenant-owned for index leading tenant_id
    public long? TenantId { get; set; }
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class UserRole : BaseEntity
{
    public long? TenantId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public long? AcademicYearId { get; set; } // scoped role per year
}
