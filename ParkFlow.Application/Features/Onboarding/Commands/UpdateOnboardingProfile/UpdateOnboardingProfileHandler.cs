using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;

public class UpdateOnboardingProfileHandler : IRequestHandler<UpdateOnboardingProfileCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IValidator<UpdateOnboardingProfileCommand> _validator;

    public UpdateOnboardingProfileHandler(
        IUserAccountRepository userAccountRepository,
        IUserProfileRepository userProfileRepository,
        IValidator<UpdateOnboardingProfileCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _userProfileRepository = userProfileRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingProfileCommand request, CancellationToken cancellationToken)
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

        user.UpdatePhoneNumber(request.PhoneNumber);
        user.UpdateOnboardingStep(OnboardingStep.Student);
        await _userAccountRepository.UpdateAsync(user);

        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (profile == null)
        {
            profile = new UserProfile(
                user.Id,
                request.FirstName,
                request.LastName,
                request.MiddleName,
                request.ProfilePictureUrl);

            await _userProfileRepository.AddAsync(profile);
        }
        else
        {
            profile.UpdateProfile(request.FirstName, request.LastName, request.MiddleName, request.ProfilePictureUrl);
            await _userProfileRepository.UpdateAsync(profile);
        }

        return Result<Guid>.Success(profile.Id, "Profile onboarding completed.");
    }
}
