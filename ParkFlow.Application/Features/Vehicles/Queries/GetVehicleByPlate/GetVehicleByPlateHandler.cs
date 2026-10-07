using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetVehicleByPlate;

public class GetVehicleByPlateHandler : IRequestHandler<GetVehicleByPlateQuery, Result<VehicleLookupDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;

    public GetVehicleByPlateHandler(
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository)
    {
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<VehicleLookupDto>> Handle(GetVehicleByPlateQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlateNumber))
        {
            return Result<VehicleLookupDto>.Failure("Plate number is required.", ErrorCode.BadRequest);
        }

        var normalizedPlate = request.PlateNumber.Trim().ToUpper();
        var vehicle = await _vehicleRepository.GetByPlateNumberAsync(normalizedPlate);

        if (vehicle == null)
        {
            return Result<VehicleLookupDto>.Success(new VehicleLookupDto(
                IsRegistered: false,
                Id: null,
                OwnerId: null,
                OwnerName: null,
                PlateNumber: normalizedPlate,
                Brand: null,
                VehicleType: null,
                VehicleTypeValue: null,
                VerificationStatus: null
            ), "Vehicle is not registered.");
        }

        var profile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        var ownerName = profile != null
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : null;

        var dto = new VehicleLookupDto(
            IsRegistered: true,
            Id: vehicle.Id,
            OwnerId: vehicle.OwnerId,
            OwnerName: string.IsNullOrWhiteSpace(ownerName) ? "Registered User" : ownerName,
            PlateNumber: vehicle.PlateNumber,
            Brand: vehicle.Brand,
            VehicleType: vehicle.VehicleType.ToString(),
            VehicleTypeValue: (int)vehicle.VehicleType,
            VerificationStatus: vehicle.VerificationStatus.ToString()
        );

        return Result<VehicleLookupDto>.Success(dto, "Vehicle details retrieved.");
    }
}
