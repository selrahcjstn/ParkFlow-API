using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;

public class UpdateOnboardingPersonnelHandler : IRequestHandler<UpdateOnboardingPersonnelCommand, Result<Guid>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<UpdateOnboardingPersonnelCommand> _validator;

    public UpdateOnboardingPersonnelHandler(
        IUserProfileRepository userProfileRepository,
        IPersonnelRepository personnelRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingPersonnelCommand> validator)
    {
        _userProfileRepository = userProfileRepository;
        _personnelRepository = personnelRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingPersonnelCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (profile == null)
            return Result<Guid>.Failure("User profile not found.", ErrorCode.NotFound);

        var existingPersonnel = await _personnelRepository.GetByUserProfileIdAsync(profile.Id);
        if (existingPersonnel == null)
        {
            var personnel = new Personnel(profile, request.IdCardNumber, request.Department);
            await _personnelRepository.AddAsync(personnel);
            existingPersonnel = personnel;
        }
        else
        {
            existingPersonnel.UpdateDetails(request.IdCardNumber, request.Department);
            await _personnelRepository.UpdateAsync(existingPersonnel);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Vehicle);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<Guid>.Success(existingPersonnel.UserProfileId, "Personnel onboarding completed.");
    }
}
