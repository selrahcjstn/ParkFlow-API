using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;

public class RegisterManualAccountValidator : AbstractValidator<RegisterManualAccountCommand>
{
    public RegisterManualAccountValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}
