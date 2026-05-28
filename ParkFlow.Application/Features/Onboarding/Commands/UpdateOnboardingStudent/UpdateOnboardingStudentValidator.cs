using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;

public class UpdateOnboardingStudentValidator : AbstractValidator<UpdateOnboardingStudentCommand>
{
    public UpdateOnboardingStudentValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StudentNumber).NotEmpty();
        RuleFor(x => x.Course).NotEmpty();
        RuleFor(x => x.Section).NotEmpty();
        RuleFor(x => x.YearLevel).GreaterThan(0);
    }
}
