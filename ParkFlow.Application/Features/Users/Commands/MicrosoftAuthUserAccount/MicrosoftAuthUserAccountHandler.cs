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
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IJwtService _jwtService;
    private readonly IValidator<MicrosoftAuthUserAccountCommand> _validator;

    public MicrosoftAuthUserAccountHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IUserProfileRepository userProfileRepository,
        IJwtService jwtService,
        IValidator<MicrosoftAuthUserAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _userProfileRepository = userProfileRepository;
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
            }

            var identity = AuthIdentity.CreateMicrosoft(user.Id, request.Email, request.ExternalProviderId);

            await _authIdentityRepository.AddAsync(identity);
        }

        string? resolvedFirstName = request.FirstName;
        string? resolvedLastName = request.LastName;

        // Try to fetch the user's actual profile if it exists
        var profile = await _userProfileRepository.GetByUserIdAsync(user.Id);
        if (profile != null)
        {
            resolvedFirstName = profile.FirstName;
            resolvedLastName = profile.LastName;
        }
        else
        {
            // If the profile doesn't exist yet, but we have a DisplayName and empty first/last name, try parsing the DisplayName as a fallback.
            if (string.IsNullOrWhiteSpace(resolvedFirstName) && string.IsNullOrWhiteSpace(resolvedLastName) && !string.IsNullOrWhiteSpace(request.DisplayName))
            {
                var displayName = request.DisplayName.Trim();
                var lastSpaceIndex = displayName.LastIndexOf(' ');
                if (lastSpaceIndex >= 0)
                {
                    resolvedFirstName = displayName.Substring(0, lastSpaceIndex).Trim();
                    resolvedLastName = displayName.Substring(lastSpaceIndex + 1).Trim();
                }
                else
                {
                    resolvedFirstName = displayName;
                    resolvedLastName = string.Empty;
                }
            }
        }

        var profileType = "unassigned";
        if (user.UserProfile != null)
        {
            profileType =
                user.UserProfile.Student != null ? "student" :
                user.UserProfile.Personnel != null ? "personnel" :
                user.UserProfile.Guard != null ? "guard" :
                "unassigned";
        }

        var token = _jwtService.GenerateToken(user, profileType);
        var isStillNewAccount = user.OnboardingStep != OnboardingStep.Done;

        var response = new MicrosoftAuthResultDto(
            user.Id,
            user.Email,
            AuthProvider.Microsoft.ToString(),
            request.ExternalProviderId,
            token,
            isStillNewAccount,
            user.OnboardingStep,
            resolvedFirstName,
            resolvedLastName,
            request.DisplayName);

        return Result<MicrosoftAuthResultDto>.Success(response, "Microsoft authentication successful.");
    }
}
