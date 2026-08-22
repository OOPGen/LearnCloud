using FluentValidation;
using LearnCloud.Auth.DTOs;

namespace LearnCloud.Auth.Validators;

public class RegisterTenantValidator : AbstractValidator<RegisterTenantRequest>
{
    public RegisterTenantValidator()
    {
        RuleFor(x => x.SchoolName).NotEmpty().MinimumLength(3).MaximumLength(255);
        RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]{3,50}$")
            .WithMessage("Slug must be 3-50 lowercase letters, numbers, hyphen");
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactPhone).NotEmpty().Matches(@"^\+?[0-9\s\-]{8,20}$");
        RuleFor(x => x.LearnerCountBand).NotEmpty().Must(b => new[] { "150-300","301-800","801-2000","2000+" }.Contains(b));
        RuleFor(x => x.AdminFullName).NotEmpty().MinimumLength(3).MaximumLength(255);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.AdminPhone).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Must(Complexity).WithMessage("Password must have upper, lower, digit, non-alphanumeric");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password);
    }

    private bool Complexity(string p)
    {
        if (string.IsNullOrEmpty(p)) return false;
        bool hasUpper = p.Any(char.IsUpper);
        bool hasLower = p.Any(char.IsLower);
        bool hasDigit = p.Any(char.IsDigit);
        bool hasNonAlpha = p.Any(ch => !char.IsLetterOrDigit(ch));
        return hasUpper && hasLower && hasDigit && hasNonAlpha;
    }
}

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(1);
        RuleFor(x => x.TenantSlug).Matches("^[a-z0-9-]{3,50}$").When(x => !string.IsNullOrEmpty(x.TenantSlug));
    }
}

public class RefreshValidator : AbstractValidator<RefreshRequest>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MinimumLength(20);
    }
}

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Must(p => p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit) && p.Any(c=>!char.IsLetterOrDigit(c)))
            .WithMessage("Password complexity required");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Must(p => p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit) && p.Any(c=>!char.IsLetterOrDigit(c)));
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
    }
}

public class VerifyEmailValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
    }
}
