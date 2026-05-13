using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

public class RegisterUserAggregateValidator : AbstractValidator<RegisterUserAggregateCommand>
{
    public RegisterUserAggregateValidator()
    {
        RuleFor(x => x.Account).NotNull().WithMessage("Account is required.")
            .ChildRules(account =>
            {
                account.RuleFor(a => a.Email).NotEmpty().EmailAddress();
                account.RuleFor(a => a.Password).NotEmpty().MinimumLength(6);
                account.RuleFor(a => a.PhoneNumber).NotEmpty();
            });

        RuleFor(x => x.Profile).NotNull().ChildRules(profile =>
        {
            profile.RuleFor(p => p.IdCardNumber).NotEmpty();
            profile.RuleFor(p => p.FirstName).NotEmpty();
            profile.RuleFor(p => p.LastName).NotEmpty();
        });

        RuleFor(x => x.CorSubmission).NotNull().DependentRules(() =>
        {
            RuleFor(x => x.CorSubmission!.AcademicTerm).NotEmpty();
            RuleFor(x => x.CorSubmission!.CorDocumentUrl).NotEmpty();
        });
    }
}
