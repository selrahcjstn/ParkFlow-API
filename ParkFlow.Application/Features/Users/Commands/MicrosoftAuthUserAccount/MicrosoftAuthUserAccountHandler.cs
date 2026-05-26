using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;

public class MicrosoftAuthUserAccountHandler : IRequestHandler<MicrosoftAuthUserAccountCommand, Result<MicrosoftAuthResultDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IJwtService _jwtService;
    private readonly IValidator<MicrosoftAuthUserAccountCommand> _validator;

    public MicrosoftAuthUserAccountHandler(
        IUserAccountRepository userAccountRepository,
        IJwtService jwtService,
        IValidator<MicrosoftAuthUserAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
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

        var existingByProvider = await _userAccountRepository.GetByAuthProviderExternalIdAsync(
            AuthProvider.Microsoft,
            request.ExternalProviderId);

        var isNewAccount = false;
        var user = existingByProvider;

        if (user is null)
        {
            var existingByEmail = await _userAccountRepository.GetByEmailAsync(request.Email);
            if (existingByEmail is not null)
            {
                return Result<MicrosoftAuthResultDto>.Failure(
                    "An account with this email already exists. Please login with your original method.",
                    ErrorCode.Conflict);
            }

            user = UserAccount.CreateMicrosoft(request.Email, request.ExternalProviderId);
            await _userAccountRepository.AddAsync(user);
            isNewAccount = true;
        }

        var token = _jwtService.GenerateToken(user, "unassigned");
        var response = new MicrosoftAuthResultDto(
            user.Id,
            user.Email,
            user.AuthProvider.ToString(),
            user.ExternalProviderId ?? request.ExternalProviderId,
            token,
            isNewAccount,
            request.FirstName,
            request.LastName,
            request.DisplayName);

        return Result<MicrosoftAuthResultDto>.Success(response, "Microsoft authentication successful.");
    }
}
