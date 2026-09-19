using FluentValidation;
using LearnCloud.Infrastructure.Email;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.PlatformAdmin.Controllers;

/// <summary>
/// A demo request or contact message from the marketing site. Belongs to the platform, not
/// to a school. The marketing site's forms used to post to an address that did not exist
/// (demo) or only show "Message sent" without sending anything (contact).
/// </summary>
public class SalesEnquiry : BaseEntity
{
    public string Source { get; set; } = null!;
    public string SchoolName { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Role { get; set; }
    public string? LearnerCount { get; set; }
    public string? CurrentSystem { get; set; }
    public string? Message { get; set; }
    public bool ConsentGiven { get; set; }
    public string Status { get; set; } = "new"; // new, contacted, closed
}

public sealed class SalesEnquiryConfiguration : IEntityTypeConfiguration<SalesEnquiry>
{
    public void Configure(EntityTypeBuilder<SalesEnquiry> b)
    {
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.SchoolName).HasMaxLength(150);
        b.Property(x => x.ContactName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Role).HasMaxLength(50);
        b.Property(x => x.LearnerCount).HasMaxLength(50);
        b.Property(x => x.CurrentSystem).HasMaxLength(100);
        b.Property(x => x.Message).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("idx_sales_enquiries_status");
    }
}

public sealed class SalesOptions
{
    /// <summary>Inbox that receives new enquiries (section Sales). Empty: enquiries are only stored.</summary>
    public string? NotificationEmail { get; set; }
}

public record SalesEnquiryRequest(
    string SchoolName, string ContactName, string Email, string? Phone, string? Role, string? LearnerCount,
    string? CurrentSystem, string? Message, bool Consent, string? Source);

public record SalesEnquiryDto(long Id, string Source, string SchoolName, string ContactName, string Email, string? Phone, string? Role,
    string? LearnerCount, string? CurrentSystem, string? Message, bool ConsentGiven, string Status, DateTime CreatedAt);

public class SalesEnquiryValidator : AbstractValidator<SalesEnquiryRequest>
{
    public const string DemoSource = "marketing_book_demo";
    public static readonly string[] Sources = { DemoSource, "marketing_contact" };

    public SalesEnquiryValidator()
    {
        // Single-line fields end up in the sales email's subject and text, so no line breaks
        // or other control characters.
        const string oneLine = "Use a single line of text.";
        RuleFor(x => x.SchoolName).NotEmpty().MaximumLength(150).Must(SingleLine).WithMessage(oneLine);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(100).Must(SingleLine).WithMessage(oneLine);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200)
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Enter a valid email address.");
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^\+?[0-9 ()-]{7,30}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Role).MaximumLength(50).Must(SingleLine).WithMessage(oneLine);
        RuleFor(x => x.LearnerCount).MaximumLength(50).Must(SingleLine).WithMessage(oneLine);
        RuleFor(x => x.CurrentSystem).MaximumLength(100).Must(SingleLine).WithMessage(oneLine);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.Source).Must(s => s is null || Sources.Contains(s)).WithMessage("Unknown form.");
        // The demo form asks for consent to be contacted about the demo. A request without a
        // source is stored as a demo request, so it needs consent too.
        RuleFor(x => x.Consent).Equal(true).When(x => (x.Source ?? DemoSource) == DemoSource).WithMessage("Please agree to be contacted about your demo.");
    }

    private static bool SingleLine(string? value) => value is null || !value.Any(char.IsControl);
}

[ApiController]
public class EnquiriesController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly IEmailOutbox _email;
    private readonly SalesOptions _sales;
    private readonly ILogger<EnquiriesController> _logger;

    public EnquiriesController(LearnCloudDbContext db, IEmailOutbox email, IOptions<SalesOptions> sales, ILogger<EnquiriesController> logger)
    {
        _db = db;
        _email = email;
        _sales = sales.Value;
        _logger = logger;
    }

    /// <summary>Public endpoint for the marketing site's forms (through the marketing Worker).</summary>
    [HttpPost("api/public/enquiries")]
    [AllowAnonymous]
    [EnableRateLimiting("public_form")]
    public async Task<IActionResult> Submit([FromBody] SalesEnquiryRequest req, CancellationToken ct)
    {
        var enquiry = new SalesEnquiry
        {
            Source = req.Source ?? SalesEnquiryValidator.DemoSource,
            SchoolName = req.SchoolName.Trim(),
            ContactName = req.ContactName.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            Phone = Blank(req.Phone),
            Role = Blank(req.Role),
            LearnerCount = Blank(req.LearnerCount),
            CurrentSystem = Blank(req.CurrentSystem),
            Message = Blank(req.Message),
            ConsentGiven = req.Consent,
        };
        _db.Set<SalesEnquiry>().Add(enquiry);

        if (!string.IsNullOrWhiteSpace(_sales.NotificationEmail))
        {
            var kind = enquiry.Source == "marketing_contact" ? "Contact message" : "Demo request";
            var lines = new List<string>
            {
                $"{kind} from {enquiry.ContactName} ({enquiry.Role ?? "role not given"}) at {enquiry.SchoolName}.",
                $"Email: {enquiry.Email}. Phone: {enquiry.Phone ?? "not given"}.",
                $"Learners: {enquiry.LearnerCount ?? "not given"}. Current system: {enquiry.CurrentSystem ?? "not given"}.",
            };
            if (enquiry.Message is not null) lines.Add($"Message: {enquiry.Message}");
            var (html, text) = EmailLayout.Render($"New {kind.ToLowerInvariant()}: {enquiry.SchoolName}", lines);
            _email.Queue(new OutgoingEmail(_sales.NotificationEmail, null, $"{kind}: {enquiry.SchoolName}", html, text, ReplyTo: enquiry.Email, Category: "sales-enquiry"), tenantId: null);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Sales enquiry {Id} received from the {Source} form", enquiry.Id, enquiry.Source);
        return Accepted(new { received = true });
    }

    /// <summary>Enquiries for platform staff, newest first.</summary>
    [HttpGet("api/platform/enquiries")]
    [Authorize(Roles = "PLATFORM_SUPERADMIN,PLATFORM_SUPPORT")]
    [EnableRateLimiting("api_general")]
    public async Task<ActionResult<List<SalesEnquiryDto>>> List([FromQuery] string? status, CancellationToken ct) =>
        await _db.Set<SalesEnquiry>()
            .Where(e => status == null || e.Status == status)
            .OrderByDescending(e => e.CreatedAt).Take(200)
            .Select(e => new SalesEnquiryDto(e.Id, e.Source, e.SchoolName, e.ContactName, e.Email, e.Phone, e.Role, e.LearnerCount, e.CurrentSystem, e.Message, e.ConsentGiven, e.Status, e.CreatedAt))
            .ToListAsync(ct);

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
