using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.LinkMicrosoftIdentity;

public class LinkMicrosoftIdentityValidator : AbstractValidator<LinkMicrosoftIdentityCommand>
{
    public LinkMicrosoftIdentityValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ExternalProviderId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
