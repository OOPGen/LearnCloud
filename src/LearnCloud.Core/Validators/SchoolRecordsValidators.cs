using FluentValidation;
using LearnCloud.Core.DTOs;

namespace LearnCloud.Core.Validators;

// Request shape rules, run by the API's validation filter before the controller (422 on
// failure). Rules that need the database (uniqueness, overlaps, capacity) are in the services.

internal static class Rules
{
    public const string PhonePattern = @"^\+?[0-9 ()-]{7,20}$";
    public const string StudentNumberPattern = @"^[A-Za-z0-9][A-Za-z0-9/-]{0,29}$";
    public static readonly DateTime Earliest = new(1900, 1, 1);

    public static bool IsOneOf(string? value, string[] allowed) => value is not null && allowed.Contains(value);
}

public class CreateAcademicYearValidator : AbstractValidator<CreateAcademicYearRequest>
{
    public CreateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the year, e.g. 2026 (up to 50 characters).");
        RuleFor(x => x.StartDate).GreaterThan(Rules.Earliest).WithMessage("Enter the start date.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The year must end after it starts.");
        RuleFor(x => x).Must(x => (x.EndDate.Date - x.StartDate.Date).TotalDays <= 550).OverridePropertyName("EndDate").WithMessage("An academic year can be at most 18 months long.");
    }
}

public class UpdateAcademicYearValidator : AbstractValidator<UpdateAcademicYearRequest>
{
    public UpdateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the year, e.g. 2026 (up to 50 characters).");
        RuleFor(x => x.StartDate).GreaterThan(Rules.Earliest).WithMessage("Enter the start date.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The year must end after it starts.");
        RuleFor(x => x).Must(x => (x.EndDate.Date - x.StartDate.Date).TotalDays <= 550).OverridePropertyName("EndDate").WithMessage("An academic year can be at most 18 months long.");
    }
}

public class CreateTermValidator : AbstractValidator<CreateTermRequest>
{
    public CreateTermValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the term, e.g. Term 1.");
        RuleFor(x => x.TermNumber).InclusiveBetween(1, 6).WithMessage("Term number must be between 1 and 6.");
        RuleFor(x => x.StartDate).GreaterThan(Rules.Earliest).WithMessage("Enter the start date.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The term must end after it starts.");
    }
}

public class UpdateTermValidator : AbstractValidator<UpdateTermRequest>
{
    public UpdateTermValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the term, e.g. Term 1.");
        RuleFor(x => x.TermNumber).InclusiveBetween(1, 6).WithMessage("Term number must be between 1 and 6.");
        RuleFor(x => x.StartDate).GreaterThan(Rules.Earliest).WithMessage("Enter the start date.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The term must end after it starts.");
    }
}

public class CreateGradeValidator : AbstractValidator<CreateGradeRequest>
{
    public CreateGradeValidator()
    {
        RuleFor(x => x.AcademicYearId).GreaterThan(0).WithMessage("Choose the academic year.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the grade, e.g. Form 1 (up to 50 characters).");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$").WithMessage("Code: up to 20 letters, digits, - or _, e.g. F1.");
        RuleFor(x => x.LevelOrder).InclusiveBetween(0, 100).WithMessage("Level must be between 0 and 100.");
    }
}

public class UpdateGradeValidator : AbstractValidator<UpdateGradeRequest>
{
    public UpdateGradeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the grade, e.g. Form 1 (up to 50 characters).");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$").WithMessage("Code: up to 20 letters, digits, - or _, e.g. F1.");
        RuleFor(x => x.LevelOrder).InclusiveBetween(0, 100).WithMessage("Level must be between 0 and 100.");
    }
}

public class CreateStreamValidator : AbstractValidator<CreateStreamRequest>
{
    public CreateStreamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the stream, e.g. Blue (up to 50 characters).");
        RuleFor(x => x.Capacity).InclusiveBetween(1, 500).WithMessage("Capacity must be between 1 and 500.");
    }
}

public class UpdateStreamValidator : AbstractValidator<UpdateStreamRequest>
{
    public UpdateStreamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Name the stream, e.g. Blue (up to 50 characters).");
        RuleFor(x => x.Capacity).InclusiveBetween(1, 500).WithMessage("Capacity must be between 1 and 500.");
    }
}

public class StudentListRequestValidator : AbstractValidator<StudentListRequest>
{
    public StudentListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy).Must(s => Rules.IsOneOf(s, new[] { "name", "student_number", "class", "created_at" })).When(x => x.SortBy != null)
            .WithMessage("Sort by name, student_number, class or created_at.");
        RuleFor(x => x.Status).Must(s => Rules.IsOneOf(s, EnrolmentValues.StudentStatuses)).When(x => !string.IsNullOrEmpty(x.Status))
            .WithMessage("Unknown student status.");
    }
}

public class CreateStudentValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("Enter the first name (up to 100 characters).");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Enter the last name (up to 100 characters).");
        RuleFor(x => x.Dob).Must(d => d!.Value.Date <= DateTime.UtcNow.Date && d.Value > Rules.Earliest).When(x => x.Dob.HasValue).WithMessage("Date of birth cannot be in the future.");
        RuleFor(x => x.Gender).Must(g => Rules.IsOneOf(g, EnrolmentValues.Genders)).When(x => !string.IsNullOrEmpty(x.Gender)).WithMessage("Gender: female, male or other.");
        RuleFor(x => x.NationalId).MaximumLength(30);
        RuleFor(x => x.StudentNumber).Matches(Rules.StudentNumberPattern).When(x => !string.IsNullOrWhiteSpace(x.StudentNumber))
            .WithMessage("Student number: up to 30 letters, digits, / or -. Leave it blank to generate one.");
        RuleFor(x => x.StreamId).GreaterThan(0).WithMessage("Choose a class.");
        RuleFor(x => x.EnrolmentType).Must(t => Rules.IsOneOf(t, EnrolmentValues.AdmissionTypes)).When(x => x.EnrolmentType != null).WithMessage("Enrolment type: new or transfer.");
        RuleFor(x => x.Guardian!).SetValidator(new LinkGuardianValidator()).When(x => x.Guardian != null);
    }
}

public class UpdateStudentValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("Enter the first name (up to 100 characters).");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Enter the last name (up to 100 characters).");
        RuleFor(x => x.Dob).Must(d => d!.Value.Date <= DateTime.UtcNow.Date && d.Value > Rules.Earliest).When(x => x.Dob.HasValue).WithMessage("Date of birth cannot be in the future.");
        RuleFor(x => x.Gender).Must(g => Rules.IsOneOf(g, EnrolmentValues.Genders)).When(x => !string.IsNullOrEmpty(x.Gender)).WithMessage("Gender: female, male or other.");
        RuleFor(x => x.NationalId).MaximumLength(30);
        RuleFor(x => x.StudentNumber).NotEmpty().Matches(Rules.StudentNumberPattern).WithMessage("Student number: up to 30 letters, digits, / or -.");
    }
}

public class ChangeEnrolmentValidator : AbstractValidator<ChangeEnrolmentRequest>
{
    public ChangeEnrolmentValidator()
    {
        RuleFor(x => x.StreamId).GreaterThan(0).WithMessage("Choose a class.");
    }
}

public class ExitStudentValidator : AbstractValidator<ExitStudentRequest>
{
    public ExitStudentValidator()
    {
        RuleFor(x => x.Reason).Must(r => Rules.IsOneOf(r, EnrolmentValues.ExitReasons)).WithMessage("Reason: withdrawn, transferred_out or graduated.");
    }
}

public class GuardianInputValidator : AbstractValidator<GuardianInput>
{
    public GuardianInputValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("Enter the guardian's first name.");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Enter the guardian's last name.");
        RuleFor(x => x.Phone).NotEmpty().Matches(Rules.PhonePattern).WithMessage("Enter a phone number, e.g. +263 77 123 4567.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Enter a valid email address.");
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.NationalId).MaximumLength(30);
    }
}

public class LinkGuardianValidator : AbstractValidator<LinkGuardianRequest>
{
    public LinkGuardianValidator()
    {
        RuleFor(x => x).Must(x => (x.GuardianId.HasValue) != (x.NewGuardian != null)).OverridePropertyName("GuardianId")
            .WithMessage("Choose an existing guardian or enter a new one, not both.");
        RuleFor(x => x.GuardianId).GreaterThan(0).When(x => x.GuardianId.HasValue);
        RuleFor(x => x.NewGuardian!).SetValidator(new GuardianInputValidator()).When(x => x.NewGuardian != null);
        RuleFor(x => x.RelationshipType).Must(r => Rules.IsOneOf(r, EnrolmentValues.Relationships))
            .WithMessage("Relationship: mother, father, guardian, grandparent, sibling, aunt, uncle or other.");
    }
}

public class UpdateGuardianLinkValidator : AbstractValidator<UpdateGuardianLinkRequest>
{
    public UpdateGuardianLinkValidator()
    {
        RuleFor(x => x.RelationshipType).Must(r => Rules.IsOneOf(r, EnrolmentValues.Relationships))
            .WithMessage("Relationship: mother, father, guardian, grandparent, sibling, aunt, uncle or other.");
    }
}

public class GuardianListRequestValidator : AbstractValidator<GuardianListRequest>
{
    public GuardianListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
