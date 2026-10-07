using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetVehicleByPlate;

public record VehicleLookupDto(
    bool IsRegistered,
    Guid? Id,
    Guid? OwnerId,
    string? OwnerName,
    string? PlateNumber,
    string? Brand,
    string? VehicleType,
    int? VehicleTypeValue,
    string? VerificationStatus
);

public record GetVehicleByPlateQuery(string PlateNumber) : IRequest<Result<VehicleLookupDto>>;
