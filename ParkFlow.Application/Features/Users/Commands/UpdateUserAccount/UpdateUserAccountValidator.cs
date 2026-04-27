using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserAccount;

public class UpdateUserAccountValidator : AbstractValidator<UpdateUserAccountCommand>
{
    public UpdateUserAccountValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("User Id is required.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Invalid email format.");

        RuleFor(x => x.PhoneNumber)
            .MinimumLength(10)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be at least 10 digits.");

        RuleFor(x => x.Role)
            .IsInEnum()
            .When(x => x.Role.HasValue)
            .WithMessage("Invalid role selected.");
    }
}