using FluentValidation;
using LearnCloud.AttendanceTimetable.DTOs;

namespace LearnCloud.AttendanceTimetable.Validators;

public class UpdateAttendanceSettingsValidator : AbstractValidator<UpdateAttendanceSettingsRequest>
{
    public UpdateAttendanceSettingsValidator()
    {
        RuleFor(x => x.Mode).NotEmpty().Must(m => new[] { "daily", "per_period" }.Contains(m));
        RuleFor(x => x.BackdatingWindowDays).InclusiveBetween(0, 30);
        RuleFor(x => x.ChronicAbsenceThreshold).InclusiveBetween(0, 100);
    }
}

public class MarkRegisterValidator : AbstractValidator<MarkRegisterRequest>
{
    public MarkRegisterValidator()
    {
        RuleFor(x => x.GradeId).GreaterThan(0);
        RuleFor(x => x.StreamId).GreaterThan(0);
        RuleFor(x => x.AttendanceDate).NotEmpty().LessThanOrEqualTo(DateTime.UtcNow.Date.AddDays(1));
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.TermId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty().Must(items => items.Count <= 80).WithMessage("Max 80 learners per register");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.StudentId).GreaterThan(0);
            item.RuleFor(i => i.Status).NotEmpty().Must(s => new[] { "present", "absent", "late", "sick", "excused" }.Contains(s.ToLower()));
            item.RuleFor(i => i.AbsenceReason).MaximumLength(100).When(i => !string.IsNullOrEmpty(i.AbsenceReason));
            item.RuleFor(i => i.Note).MaximumLength(255);
        });
        // PeriodNumber nullable: if per-period mode, must be present
        RuleFor(x => x.PeriodNumber).GreaterThan(0).When(x => x.PeriodNumber.HasValue);
    }
}

public class GetRegisterValidator : AbstractValidator<GetRegisterRequest>
{
    public GetRegisterValidator()
    {
        RuleFor(x => x.GradeId).GreaterThan(0);
        RuleFor(x => x.StreamId).GreaterThan(0);
        RuleFor(x => x.AttendanceDate).NotEmpty();
    }
}

public class CreatePeriodValidator : AbstractValidator<CreatePeriodRequest>
{
    public CreatePeriodValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.StartTime).NotEmpty().Matches(@"^\d{2}:\d{2}$");
        RuleFor(x => x.EndTime).NotEmpty().Matches(@"^\d{2}:\d{2}$");
        RuleFor(x => x).Must(x =>
        {
            if (TimeSpan.TryParse(x.StartTime, out var s) && TimeSpan.TryParse(x.EndTime, out var e))
                return s < e;
            return false;
        }).WithMessage("StartTime must be before EndTime");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
