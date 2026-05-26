using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;

public record UpdateOnboardingProfileCommand(
    Guid UserId,
    string PhoneNumber,
    string FirstName,
    string LastName,
    string? ProfilePictureUrl) : IRequest<Result<Guid>>;
