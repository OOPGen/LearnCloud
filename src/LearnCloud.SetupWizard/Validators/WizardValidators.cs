using FluentValidation;
using LearnCloud.SetupWizard.DTOs;

namespace LearnCloud.SetupWizard.Validators;

public class SchoolProfileValidator : AbstractValidator<SchoolProfileDto>
{
    public SchoolProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(3).MaximumLength(255);
        RuleFor(x => x.Type).NotEmpty().Must(t => new[] { "primary", "secondary", "combined", "early_years" }.Contains(t));
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().Length(2);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactPhone).NotEmpty().Matches(@"^\+?[0-9\s\-]{8,20}$");
        RuleFor(x => x.Timezone).NotEmpty().Must(tz => tz == "Africa/Harare" || TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == tz)).WithMessage("Invalid timezone, default Africa/Harare");
        RuleFor(x => x.BaseCurrency).NotEmpty().Must(c => new[] { "USD","ZWG","ZAR" }.Contains(c));
        RuleFor(x => x.LearnerCountBand).NotEmpty();
    }
}

public class BrandingValidator : AbstractValidator<BrandingDto>
{
    public BrandingValidator()
    {
        RuleFor(x => x.PrimaryColor).NotEmpty().Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$").WithMessage("Primary color must be hex #0F153A");
        RuleFor(x => x.SecondaryColor).NotEmpty().Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
        RuleFor(x => x.LogoUrl).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.LogoUrl));
    }
}

public class AcademicYearValidator : AbstractValidator<AcademicYearDto>
{
    public AcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Matches(@"^\d{4}$").WithMessage("Year name 2026");
        RuleFor(x => x.StartDate).NotEmpty().LessThan(x => x.EndDate).WithMessage("Start must be before end");
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate);
        RuleFor(x => x).Must(x => (x.EndDate - x.StartDate).TotalDays >= 200 && (x.EndDate - x.StartDate).TotalDays <= 400).WithMessage("Academic year should be 200-400 days");
    }
}

public class TermsSetupValidator : AbstractValidator<TermsSetupDto>
{
    public TermsSetupValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(2, 4).WithMessage("Terms count 2-4, default 3");
        RuleFor(x => x.Terms).NotEmpty().Must((dto, terms) => terms.Count == dto.Count).WithMessage("Terms list count must match Count");
        RuleForEach(x => x.Terms).SetValidator(new TermValidator());
        RuleFor(x => x).Must(HaveNoOverlap).WithMessage("Terms must not overlap and must be within academic year");
    }

    private bool HaveNoOverlap(TermsSetupDto dto)
    {
        var ordered = dto.Terms.OrderBy(t => t.StartDate).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].StartDate < ordered[i - 1].EndDate) return false;
        }
        return true;
    }
}

public class TermValidator : AbstractValidator<TermDto>
{
    public TermValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TermNumber).InclusiveBetween(1, 4);
        RuleFor(x => x.StartDate).NotEmpty().LessThan(x => x.EndDate);
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate);
    }
}

public class ClassesAndStreamsValidator : AbstractValidator<ClassesAndStreamsDto>
{
    public ClassesAndStreamsValidator()
    {
        RuleFor(x => x.Classes).NotEmpty().Must(c => c.Count <= 20).WithMessage("Max 20 grades at once");
        RuleForEach(x => x.Classes).ChildRules(cls =>
        {
            cls.RuleFor(c => c.GradeName).NotEmpty().MaximumLength(50);
            cls.RuleFor(c => c.GradeCode).NotEmpty().MaximumLength(20);
            cls.RuleFor(c => c.Streams).NotEmpty().Must(s => s.Count <= 10).WithMessage("Max 10 streams per grade");
            cls.RuleForEach(c => c.Streams).ChildRules(st =>
            {
                st.RuleFor(s => s.Name).NotEmpty().MaximumLength(50);
                st.RuleFor(s => s.Capacity).InclusiveBetween(10, 200);
            });
        });
    }
}

public class SubjectsValidator : AbstractValidator<SubjectsDto>
{
    public SubjectsValidator()
    {
        RuleFor(x => x.Subjects).NotEmpty().Must(s => s.Count <= 50);
        RuleForEach(x => x.Subjects).ChildRules(sub =>
        {
            sub.RuleFor(s => s.Name).NotEmpty().MaximumLength(100).When(s => s.Selected);
            sub.RuleFor(s => s.Code).NotEmpty().MaximumLength(20).When(s => s.Selected);
        });
    }
}

public class DepartmentsValidator : AbstractValidator<DepartmentsAndRolesDto>
{
    public DepartmentsValidator()
    {
        RuleFor(x => x.Departments).Must(d => d.Count <= 20);
        RuleForEach(x => x.Departments).ChildRules(dep =>
        {
            dep.RuleFor(d => d.Name).NotEmpty().MaximumLength(100);
        });
    }
}

public class GradingScaleValidator : AbstractValidator<GradingScaleDto>
{
    public GradingScaleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Bands).NotEmpty().Must(b => b.Count >= 3 && b.Count <= 12).WithMessage("3-12 grading bands");
        RuleForEach(x => x.Bands).ChildRules(band =>
        {
            band.RuleFor(b => b.Symbol).NotEmpty().MaximumLength(10);
            band.RuleFor(b => b.MinScore).InclusiveBetween(0, 100);
            band.RuleFor(b => b.MaxScore).InclusiveBetween(0, 100).GreaterThan(b => b.MinScore);
        });
        RuleFor(x => x).Must(NoOverlapAndCover).WithMessage("Bands must not overlap and should cover 0-100 without major gaps");
    }

    private bool NoOverlapAndCover(GradingScaleDto scale)
    {
        var ordered = scale.Bands.OrderBy(b => b.MinScore).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].MinScore < ordered[i - 1].MaxScore) return false; // overlap
        }
        // Check first starts at 0 and last ends at 100 (allow small gap)
        return ordered.First().MinScore == 0 && ordered.Last().MaxScore == 100;
    }
}

public class PreferencesValidator : AbstractValidator<PreferencesDto>
{
    public PreferencesValidator()
    {
        RuleFor(x => x.WeekStart).NotEmpty().Must(w => new[] { "Monday", "Sunday" }.Contains(w));
        RuleFor(x => x.AttendanceMode).NotEmpty().Must(m => new[] { "daily", "per_period" }.Contains(m));
        RuleFor(x => x.InvoiceNumberPrefix).NotEmpty().MaximumLength(10).Matches("^[A-Z0-9-]+$");
        RuleFor(x => x.InvoiceNextNumber).InclusiveBetween(1, 1000000);
        RuleFor(x => x.InvoiceNumberFormat).NotEmpty().Must(f => f.Contains("{prefix}") && f.Contains("{number}")).WithMessage("Format must contain {prefix} and {number}, e.g. {prefix}-{year}-{number:5}");
    }
}
