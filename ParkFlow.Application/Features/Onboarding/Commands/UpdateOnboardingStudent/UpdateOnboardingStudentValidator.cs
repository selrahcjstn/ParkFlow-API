using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;

public class UpdateOnboardingStudentValidator : AbstractValidator<UpdateOnboardingStudentCommand>
{
    public UpdateOnboardingStudentValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StudentNumber).NotEmpty();
        When(x => x.YearLevel < 7 || x.YearLevel > 10, () =>
        {
            RuleFor(x => x.Course).NotEmpty().WithMessage("Course is required for senior high and college students.");
        });
        RuleFor(x => x.Section).NotEmpty();
        RuleFor(x => x.YearLevel).GreaterThan(0);
    }
}
