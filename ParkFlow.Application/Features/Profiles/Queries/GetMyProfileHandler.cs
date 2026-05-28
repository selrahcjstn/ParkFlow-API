using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Profiles.Queries;

public class GetMyProfileHandler
    : IRequestHandler<GetMyProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IUserContext _userContext;

    public GetMyProfileHandler(
        IUserProfileRepository userProfileRepository,
        IUserContext userContext)
    {
        _userProfileRepository = userProfileRepository;
        _userContext = userContext;
    }

    public async Task<Result<UserProfileDto>> Handle(
        GetMyProfileQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetUserId();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return Result<UserProfileDto>.Failure("User profile not found.", ErrorCode.NotFound);

        var dto = new UserProfileDto(
            profile.Id,
            profile.UserAccountId,
            profile.UserAccount.PhoneNumber,
            profile.FirstName,
            profile.LastName,
            profile.ProfilePictureUrl,
            profile.UserAccount.OnboardingStep);

        return Result<UserProfileDto>.Success(dto, "User profile retrieved.");
    }
}