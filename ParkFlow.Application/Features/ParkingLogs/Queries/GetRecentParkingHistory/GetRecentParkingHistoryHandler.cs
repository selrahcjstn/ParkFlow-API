using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;

public class GetRecentParkingHistoryHandler : IRequestHandler<GetRecentParkingHistoryQuery, Result<IEnumerable<ParkingLogActivityDto>>>
{
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetRecentParkingHistoryHandler(IParkingLogRepository parkingLogRepository)
    {
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<IEnumerable<ParkingLogActivityDto>>> Handle(GetRecentParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;
        var logs = await _parkingLogRepository.GetRecentParkingLogsAsync(limit);

        var dtos = logs.Select(parkingLog =>
        {
            var transactionType = parkingLog.ExitTime.HasValue ? "Exit" : "Entry";

            return new ParkingLogActivityDto(
                parkingLog.Id,
                parkingLog.Vehicle.PlateNumber,
                parkingLog.EntryTime.Date,
                parkingLog.CreatedAt,
                parkingLog.Vehicle.Brand,
                parkingLog.Vehicle.VehicleType,
                transactionType,
                parkingLog.EntryTime,
                parkingLog.ExitTime);
        });

        return Result<IEnumerable<ParkingLogActivityDto>>.Success(dtos, "Recent parking history retrieved.");
    }
}