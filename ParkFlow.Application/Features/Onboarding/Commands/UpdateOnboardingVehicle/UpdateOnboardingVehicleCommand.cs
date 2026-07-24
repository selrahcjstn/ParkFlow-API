using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;

public record UpdateOnboardingVehicleCommand(
    Guid UserId,
    string PlateNumber,
    string Brand,
    VehicleType VehicleType,
    string? Color = null,
    string? MotorPictureUrl = null,
    string? OrcrDocumentUrl = null) : IRequest<Result<Guid>>;
