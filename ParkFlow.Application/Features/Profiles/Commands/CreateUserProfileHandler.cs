using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Profiles.Commands;

public class CreateUserProfileHandler : IRequestHandler<CreateUserProfileCommand, Result<Guid>>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public CreateUserProfileHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }
    public async Task<Result<Guid>> Handle(CreateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var userProfile = new UserProfile(request.UserId, request.FirstName, request.LastName, request.ProfilePictureUrl);

        await _userProfileRepository.AddAsync(userProfile);
        
        return Result<Guid>.Success(userProfile.Id, "User profile created.");
    }
}
