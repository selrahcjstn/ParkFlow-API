using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Profiles.Commands;

public record UpdateUserProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? ProfilePictureUrl
) : IRequest<Result<Guid>>;
