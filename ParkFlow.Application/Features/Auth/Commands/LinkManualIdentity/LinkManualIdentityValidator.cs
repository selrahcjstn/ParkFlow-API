using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.LinkManualIdentity;

public class LinkManualIdentityValidator : AbstractValidator<LinkManualIdentityCommand>
{
    public LinkManualIdentityValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}
