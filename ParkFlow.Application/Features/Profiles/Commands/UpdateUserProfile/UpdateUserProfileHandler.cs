using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Profiles.Commands.UpdateUserProfile;

public class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileCommand, Result<Guid>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IValidator<UpdateUserProfileCommand> _validator;

    public UpdateUserProfileHandler(
        IUserProfileRepository userProfileRepository,
        IValidator<UpdateUserProfileCommand> validator)
    {
        _userProfileRepository = userProfileRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
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

        profile.UpdateProfile(
            request.IdCardNumber,
            request.FirstName,
            request.LastName,
            request.ProfilePictureUrl,
            request.Course,
            request.Section,
            request.YearLevel,
            request.Office);

        await _userProfileRepository.UpdateAsync(profile);

        return Result<Guid>.Success(profile.Id, "User profile updated.");
    }
}
