using System.ComponentModel.DataAnnotations;

namespace LearnCloud.MultiTenancy.Entities;

// 1. Entities: Tenant (school)
public class Tenant : BaseEntity
{
    [MaxLength(255)] public string Name { get; set; } = null!;
    [MaxLength(100)] public string Slug { get; set; } = null!; // petra -> petra.learncloud.co.zw
    [MaxLength(20)] public string Status { get; set; } = "trial"; // trial, active, suspended, cancelled
    [MaxLength(100)] public string City { get; set; } = "Bulawayo";
    [MaxLength(2)] public string Country { get; set; } = "ZW";
    [MaxLength(255)] public string ContactEmail { get; set; } = null!;
    [MaxLength(50)] public string? ContactPhone { get; set; }
    [MaxLength(7)] public string PrimaryColor { get; set; } = "#0F153A";
    [MaxLength(20)] public string LearnerCountBand { get; set; } = "150-300";
    public string? LogoUrl { get; set; }

    // Navigation. Subscriptions are owned by LearnCloud.PlatformBilling and point
    // here by TenantId; MultiTenancy cannot reference that module.
    public ICollection<TenantDomain> Domains { get; set; } = new List<TenantDomain>();
    public TenantSettings? Settings { get; set; }
}

// TenantDomain - custom domain mapping e.g. portal.hillcrest.ac.zw
public class TenantDomain : TenantOwnedEntity
{
    [MaxLength(255)] public string Domain { get; set; } = null!; // e.g. portal.hillcrest.ac.zw or petra.learncloud.co.zw
    public bool IsPrimary { get; set; } = false;
    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }
    public bool IsCustom { get; set; } = false; // true if not *.learncloud.co.zw
}

// Plan and Subscription are owned by LearnCloud.PlatformBilling. The simpler copies
// that used to sit here duplicated them and were removed.

// TenantSettings - per-tenant config, 1-1 with Tenant
public class TenantSettings : TenantOwnedEntity
{
    [MaxLength(50)] public string TimeZone { get; set; } = "Africa/Harare";
    [MaxLength(10)] public string DateFormat { get; set; } = "DD/MM/YYYY";
    [MaxLength(3)] public string BaseCurrency { get; set; } = "USD";
    public bool ZWGCurrencyEnabled { get; set; } = true;
    [MaxLength(7)] public string PrimaryColor { get; set; } = "#0F153A";
    [MaxLength(7)] public string SecondaryColor { get; set; } = "#5F3F96";
    public string? LogoUrl { get; set; }
    public int CurrentAcademicYearId { get; set; } = 0;
    public int CurrentTermId { get; set; } = 0;
    public bool AllowCustomDomain { get; set; } = false;
    public string? FeaturesJson { get; set; }
    public string? BrandingJson { get; set; }
}
