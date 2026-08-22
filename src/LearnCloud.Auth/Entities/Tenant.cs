namespace LearnCloud.Auth.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!; // subdomain e.g. petra
    public string Status { get; set; } = "trial"; // trial, active, suspended, cancelled
    public string City { get; set; } = "Bulawayo";
    public string Country { get; set; } = "ZW";
    public string ContactEmail { get; set; } = null!;
    public string? ContactPhone { get; set; }
    public string PrimaryColor { get; set; } = "#0F153A";
    public string LearnerCountBand { get; set; } = "150-300";
    public string? LogoUrl { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
}
