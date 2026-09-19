using LearnCloud.Infrastructure.Email;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.Auth.Services;

/// <summary>
/// Account emails (verification, password reset, welcome), queued through the email outbox.
/// Links open the web app's /verify-email and /reset-password pages. The raw token only
/// travels in the email: the queued job's payload is cleared once the email is sent.
/// </summary>
public sealed class OutboxEmailSender : IEmailSender
{
    private readonly LearnCloudDbContext _db;
    private readonly IEmailOutbox _outbox;
    private readonly AppOptions _app;
    private readonly PasswordPolicyOptions _policy;

    public OutboxEmailSender(LearnCloudDbContext db, IEmailOutbox outbox, IOptions<AppOptions> app, IOptions<PasswordPolicyOptions> policy)
    {
        _db = db;
        _outbox = outbox;
        _app = app.Value;
        _policy = policy.Value;
    }

    public async Task SendEmailVerificationAsync(string email, string displayName, string rawToken, long? tenantId)
    {
        var school = await SchoolAsync(tenantId);
        var link = _app.Link("/verify-email", new Dictionary<string, string?> { ["school"] = school?.Slug, ["email"] = email, ["token"] = rawToken });
        var (html, text) = EmailLayout.Render(
            "Confirm your email address",
            new[]
            {
                $"Hello {displayName},",
                school is null
                    ? "Please confirm your email address for LearnCloud."
                    : $"{school.Name} is set up on LearnCloud. Please confirm your email address.",
                $"The link works for {_policy.EmailVerificationTokenHours} hours.",
            },
            "Confirm email address", link,
            footer: school is null ? null : $"Your school code is {school.Slug}. You need it to sign in.");
        await QueueAsync(new OutgoingEmail(email, displayName, "Confirm your LearnCloud email address", html, text, Category: "email-verification"), tenantId);
    }

    public async Task SendPasswordResetAsync(string email, string displayName, string rawToken, long? tenantId)
    {
        var school = await SchoolAsync(tenantId);
        var link = _app.Link("/reset-password", new Dictionary<string, string?> { ["school"] = school?.Slug, ["email"] = email, ["token"] = rawToken });
        var (html, text) = EmailLayout.Render(
            "Reset your password",
            new[]
            {
                $"Hello {displayName},",
                school is null
                    ? "Someone asked to reset your LearnCloud password."
                    : $"Someone asked to reset your LearnCloud password for {school.Name}.",
                $"The link works for {_policy.PasswordResetTokenHours} hours and only once. If you did not ask for this, ignore this email; your password stays the same.",
            },
            "Choose a new password", link);
        await QueueAsync(new OutgoingEmail(email, displayName, "Reset your LearnCloud password", html, text, Category: "password-reset"), tenantId);
    }

    public async Task SendWelcomeAsync(string email, string displayName, long? tenantId)
    {
        var school = await SchoolAsync(tenantId);
        var link = _app.Link("/login", new Dictionary<string, string?>());
        var (html, text) = EmailLayout.Render(
            "Welcome to LearnCloud",
            new[] { $"Hello {displayName},", school is null ? "Your LearnCloud account is ready." : $"Your account for {school.Name} is ready." },
            "Sign in", link,
            footer: school is null ? null : $"Your school code is {school.Slug}.");
        await QueueAsync(new OutgoingEmail(email, displayName, "Welcome to LearnCloud", html, text, Category: "welcome"), tenantId);
    }

    private async Task QueueAsync(OutgoingEmail email, long? tenantId)
    {
        _outbox.Queue(email, tenantId);
        await _db.SaveChangesAsync();
    }

    private sealed record School(string Slug, string Name);

    private async Task<School?> SchoolAsync(long? tenantId) =>
        tenantId is long id
            ? await _db.Tenants.Where(t => t.Id == id).Select(t => new School(t.Slug, t.Name)).FirstOrDefaultAsync()
            : null;
}
