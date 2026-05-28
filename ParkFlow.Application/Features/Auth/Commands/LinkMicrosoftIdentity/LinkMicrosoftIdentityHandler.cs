using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Auth.Commands.LinkMicrosoftIdentity;

public class LinkMicrosoftIdentityHandler : IRequestHandler<LinkMicrosoftIdentityCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IValidator<LinkMicrosoftIdentityCommand> _validator;

    public LinkMicrosoftIdentityHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IValidator<LinkMicrosoftIdentityCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(LinkMicrosoftIdentityCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

        var existingProvider = await _authIdentityRepository.GetByProviderIdAsync(AuthProvider.Microsoft, request.ExternalProviderId);
        if (existingProvider != null)
            return Result<Guid>.Failure("Microsoft account already linked to another user.", ErrorCode.Conflict);

        var existingEmail = await _authIdentityRepository.GetByEmailAsync(request.Email);
        if (existingEmail != null && existingEmail.UserAccountId != request.UserId)
            return Result<Guid>.Failure("Email is already linked to another account.", ErrorCode.Conflict);

        var identity = AuthIdentity.CreateMicrosoft(user.Id, request.Email, request.ExternalProviderId);
        await _authIdentityRepository.AddAsync(identity);

        return Result<Guid>.Success(identity.Id, "Microsoft login linked successfully.");
    }
}
