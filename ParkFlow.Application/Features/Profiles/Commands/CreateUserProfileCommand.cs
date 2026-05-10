using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Profiles.Commands;

public record CreateUserProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    String? ProfilePictureUrl
) : IRequest<Result<Guid>>;