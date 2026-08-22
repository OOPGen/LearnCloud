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
