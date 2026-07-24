using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId;

public class GetVehiclesByOwnerIdHandler : IRequestHandler<GetVehiclesByOwnerIdQuery, Result<IEnumerable<VehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public GetVehiclesByOwnerIdHandler(IVehicleRepository vehicleRepository, IParkingLogRepository parkingLogRepository)
        : this(vehicleRepository, parkingLogRepository, null!)
    {
    }

    public GetVehiclesByOwnerIdHandler(
        IVehicleRepository vehicleRepository,
        IParkingLogRepository parkingLogRepository,
        ICorSubmissionRepository corSubmissionRepository)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
        _corSubmissionRepository = corSubmissionRepository;
    }

    public async Task<Result<IEnumerable<VehicleDto>>> Handle(GetVehiclesByOwnerIdQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);
        var cor = _corSubmissionRepository != null ? await _corSubmissionRepository.GetLatestByUserIdAsync(request.OwnerId) : null;

        var vehicleDtos = new List<VehicleDto>();

        foreach (var vehicle in vehicles)
        {
            var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
            var status = activeParkingLog?.Status.ToString() ?? ParkingStatus.Exited.ToString();

            vehicleDtos.Add(new VehicleDto(
                vehicle.Id,
                vehicle.PlateNumber,
                vehicle.Brand,
                vehicle.QrCodeHash,
                vehicle.VehicleType,
                status,
                vehicle.IsPrimary,
                vehicle.Color,
                vehicle.MotorPictureUrl ?? cor?.MotorPictureUrl,
                vehicle.OrcrDocumentUrl ?? cor?.OrcrDocumentUrl));
        }

        return Result<IEnumerable<VehicleDto>>.Success(vehicleDtos, "Vehicles retrieved.");
    }
}
