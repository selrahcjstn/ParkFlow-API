using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

public class RegisterUserAggregateValidator : AbstractValidator<RegisterUserAggregateCommand>
{
    public RegisterUserAggregateValidator()
    {
        RuleFor(x => x.Account).NotNull().WithMessage("Account is required.");
        When(x => x.Account != null, () =>
        {
            RuleFor(x => x.Account.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Account.Password).NotEmpty().MinimumLength(6);
            RuleFor(x => x.Account.PhoneNumber).NotEmpty();
        });

        When(x => x.Profile != null, () =>
        {
            RuleFor(x => x.Profile!.IdCardNumber).NotEmpty();
            RuleFor(x => x.Profile!.FirstName).NotEmpty();
            RuleFor(x => x.Profile!.LastName).NotEmpty();
        });

        When(x => x.CorSubmission != null, () =>
        {
            RuleFor(x => x.CorSubmission.AcademicTerm).NotEmpty();
            RuleFor(x => x.CorSubmission.CorDocumentUrl).NotEmpty();
        });
    }
}
