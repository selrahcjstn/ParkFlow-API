using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Profiles.Commands;

public class CreateUserProfileHandler : IRequestHandler<CreateUserProfileCommand, Result<Guid>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IValidator<CreateUserProfileCommand> _validator;

    public CreateUserProfileHandler(
        IUserProfileRepository userProfileRepository,
        IValidator<CreateUserProfileCommand> validator)
    {
        _userProfileRepository = userProfileRepository;
        _validator = validator;
    }
    public async Task<Result<Guid>> Handle(CreateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var hasProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (hasProfile != null)
        {
            return Result<Guid>.Failure("User profile already exists.", ErrorCode.Conflict);
        }

        var userProfile = new UserProfile(request.UserId, request.FirstName, request.LastName, request.ProfilePictureUrl);

        await _userProfileRepository.AddAsync(userProfile);

        return Result<Guid>.Success(userProfile.Id, "User profile created.");
    }
}
