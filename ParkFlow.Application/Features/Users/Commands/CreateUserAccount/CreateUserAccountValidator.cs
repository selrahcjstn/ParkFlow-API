using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.CreateUserAccount
{
    public class CreateUserAccountValidator : AbstractValidator<CreateUserAccountCommand>
    {
        public CreateUserAccountValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Email must be a valid email address")
                .MaximumLength(255)
                .WithMessage("Email must not exceed 255 characters");

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required")
                .MinimumLength(8)
                .WithMessage("Password must be at least 8 characters long")
                .MaximumLength(128)
                .WithMessage("Password must not exceed 128 characters")
                .Matches(@"[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter")
                .Matches(@"[a-z]")
                .WithMessage("Password must contain at least one lowercase letter")
                .Matches(@"[0-9]")
                .WithMessage("Password must contain at least one digit")
                .Matches(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]")
                .WithMessage("Password must contain at least one special character");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .WithMessage("Phone number is required.")
                .Matches(@"^(\+63|0)?9\d{9}$")
                .WithMessage("Phone number must be in Philippine format (e.g., +639123456789, 09123456789)");

            RuleFor(x => x.Role)
                .NotEmpty()
                .WithMessage("Role is required")
                .IsInEnum()
                .WithMessage("Role must be a valid role");
        }
    }
}