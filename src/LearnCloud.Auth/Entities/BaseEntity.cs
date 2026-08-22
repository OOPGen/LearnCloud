namespace LearnCloud.Auth.Entities;

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

public abstract class TenantOwnedEntity : BaseEntity
{
    public long TenantId { get; set; }
}
