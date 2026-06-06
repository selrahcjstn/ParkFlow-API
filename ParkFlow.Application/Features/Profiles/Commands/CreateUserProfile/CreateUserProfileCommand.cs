using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Profiles.Commands.CreateUserProfile;

public record CreateUserProfileCommand(
    Guid UserId,
    string IdCardNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? ProfilePictureUrl
) : IRequest<Result<Guid>>;