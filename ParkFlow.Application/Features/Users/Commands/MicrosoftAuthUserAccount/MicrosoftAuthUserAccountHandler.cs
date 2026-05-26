using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;

public class MicrosoftAuthUserAccountHandler : IRequestHandler<MicrosoftAuthUserAccountCommand, Result<MicrosoftAuthResultDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IJwtService _jwtService;
    private readonly IValidator<MicrosoftAuthUserAccountCommand> _validator;

    public MicrosoftAuthUserAccountHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IJwtService jwtService,
        IValidator<MicrosoftAuthUserAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _jwtService = jwtService;
        _validator = validator;
    }

    public async Task<Result<MicrosoftAuthResultDto>> Handle(MicrosoftAuthUserAccountCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<MicrosoftAuthResultDto>.Failure(errors, ErrorCode.BadRequest);
        }

        var existingIdentity = await _authIdentityRepository.GetByProviderIdAsync(
            AuthProvider.Microsoft,
            request.ExternalProviderId);

        var isNewAccount = false;
        UserAccount? user;

        if (existingIdentity != null)
        {
            user = existingIdentity.UserAccount;
        }
        else
        {
            var existingByEmail = await _authIdentityRepository.GetByEmailAsync(request.Email);
            if (existingByEmail != null)
            {
                user = existingByEmail.UserAccount;
            }
            else
            {
                user = UserAccount.CreateMicrosoft(request.Email, request.ExternalProviderId);
                await _userAccountRepository.AddAsync(user);
                isNewAccount = true;
            }

            var identity = new AuthIdentity(
                user.Id,
                AuthProvider.Microsoft,
                request.Email,
                request.ExternalProviderId,
                null,
                true);

            await _authIdentityRepository.AddAsync(identity);
        }

        var token = _jwtService.GenerateToken(user, "unassigned");
        var response = new MicrosoftAuthResultDto(
            user.Id,
            user.Email,
            AuthProvider.Microsoft.ToString(),
            request.ExternalProviderId,
            token,
            isNewAccount,
            request.FirstName,
            request.LastName,
            request.DisplayName);

        return Result<MicrosoftAuthResultDto>.Success(response, "Microsoft authentication successful.");
    }
}
