using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Profiles.Commands.UpdateUserProfile;

public record UpdateUserProfileCommand(
    Guid UserId,
    string? IdCardNumber,
    string? FirstName,
    string? LastName,
    string? ProfilePictureUrl,
    string? Course,
    string? Section,
    int? YearLevel,
    string? Office
) : IRequest<Result<Guid>>;
