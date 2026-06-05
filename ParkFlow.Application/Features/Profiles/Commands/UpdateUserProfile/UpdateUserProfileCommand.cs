using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Profiles.Commands.UpdateUserProfile;

public record UpdateUserProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? MiddleName,
    string? ProfilePictureUrl
) : IRequest<Result<Guid>>;
