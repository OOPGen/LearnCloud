using FluentValidation;
using LearnCloud.AttendanceTimetable.DTOs;

namespace LearnCloud.AttendanceTimetable.Validators;

public class CreateTimetableValidator : AbstractValidator<CreateTimetableRequest>
{
    public CreateTimetableValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.TermId).GreaterThan(0);
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThan(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
    }
}

public class CreateSlotValidator : AbstractValidator<CreateSlotRequest>
{
    public CreateSlotValidator()
    {
        RuleFor(x => x.GradeId).GreaterThan(0);
        RuleFor(x => x.StreamId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.TeacherStaffId).GreaterThan(0);
        RuleFor(x => x.DayOfWeek).InclusiveBetween(1, 7).WithMessage("DayOfWeek 1=Mon .. 7=Sun");
        RuleFor(x => x.PeriodNumber).InclusiveBetween(1, 12);
    }
}

public class BulkSlotsValidator : AbstractValidator<BulkSlotsRequest>
{
    public BulkSlotsValidator()
    {
        RuleFor(x => x.Slots).NotEmpty().Must(s => s.Count <= 200).WithMessage("Max 200 slots per bulk save");
        RuleForEach(x => x.Slots).SetValidator(new CreateSlotValidator());
    }
}
