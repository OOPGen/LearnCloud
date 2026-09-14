namespace LearnCloud.MultiTenancy.Entities;

// BaseEntity carrying audit and soft-delete per requirements
public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }
}

// Filter for unique indexes on soft-deletable tables. Deletes are soft, so deleted rows
// stay in the table; without this filter a deleted role, grant, domain or user email
// could never be created again. Leave it off where values must never be reused
// (invoice numbers, token hashes).
public static class SoftDelete
{
    public const string ActiveRowsFilter = "is_deleted = false";
}

// ITenantEntity with TenantId discriminator - shared DB
public interface ITenantEntity
{
    long TenantId { get; set; }
}

// For platform-owned but still tenant-aware? Tenant itself not ITenantEntity
// All tenant-owned tables implement both BaseEntity + ITenantEntity via abstract class
public abstract class TenantOwnedEntity : BaseEntity, ITenantEntity
{
    public long TenantId { get; set; }
}
