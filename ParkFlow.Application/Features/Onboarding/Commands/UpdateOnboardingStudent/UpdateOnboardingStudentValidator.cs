using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;

public class UpdateOnboardingStudentValidator : AbstractValidator<UpdateOnboardingStudentCommand>
{
    public UpdateOnboardingStudentValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StudentNumber).NotEmpty().MaximumLength(50)
            .Must(number => !string.IsNullOrWhiteSpace(number) &&
                System.Text.RegularExpressions.Regex.IsMatch(Student.NormalizeNumber(number), @"^[0-9]{10}$"))
            .WithMessage("Student ID number must contain exactly 10 digits (e.g. 2023106763).");
        When(x => x.YearLevel < 7 || x.YearLevel > 10, () =>
        {
            RuleFor(x => x.Course).NotEmpty().WithMessage("Course is required for senior high and college students.");
        });
        RuleFor(x => x.Section).NotEmpty();
        RuleFor(x => x.YearLevel).GreaterThan(0);
    }
}
