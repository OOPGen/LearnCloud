using FluentValidation;
using LearnCloud.Core.DTOs;

namespace LearnCloud.Core.Validators;

public class CreateSubjectValidator : AbstractValidator<CreateSubjectRequest>
{
    public CreateSubjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100).WithMessage("Name 2-100 chars, e.g. Mathematics");
        RuleFor(x => x.Code).NotEmpty().MinimumLength(2).MaximumLength(20).Matches("^[A-Z0-9_]+$").WithMessage("Code 2-20 uppercase letters, numbers, underscore, e.g. MATH");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Department).MaximumLength(100);
    }
}

public class UpdateSubjectValidator : AbstractValidator<UpdateSubjectRequest>
{
    public UpdateSubjectValidator()
    {
        RuleFor(x => x.Name).MinimumLength(2).MaximumLength(100).When(x => x.Name != null);
        RuleFor(x => x.Code).MinimumLength(2).MaximumLength(20).Matches("^[A-Z0-9_]+$").When(x => x.Code != null);
        RuleFor(x => x.Status).Must(s => new[] { "active", "archived" }.Contains(s!)).When(x => x.Status != null);
    }
}

public class AssignSubjectToGradeValidator : AbstractValidator<AssignSubjectToGradeRequest>
{
    public AssignSubjectToGradeValidator()
    {
        RuleFor(x => x.GradeId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
    }
}

public class SubjectListRequestValidator : AbstractValidator<SubjectListRequest>
{
    public SubjectListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SortBy).Must(s => s == null || new[] { "name", "code", "department", "created_at" }.Contains(s)).When(x => x.SortBy != null);
    }
}
