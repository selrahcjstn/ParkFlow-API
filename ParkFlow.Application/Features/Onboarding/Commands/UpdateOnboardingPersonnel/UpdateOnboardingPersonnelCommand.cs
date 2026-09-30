using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;

public record UpdateOnboardingPersonnelCommand(
    Guid UserId,
    string IdCardNumber,
    string Department,
    string? Role = null) : IRequest<Result<Guid>>;

