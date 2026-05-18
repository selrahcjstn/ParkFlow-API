using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;

public class GetRecentParkingHistoryHandler : IRequestHandler<GetRecentParkingHistoryQuery, Result<IEnumerable<ParkingLogHistoryDto>>>
{
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetRecentParkingHistoryHandler(IParkingLogRepository parkingLogRepository)
    {
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<IEnumerable<ParkingLogHistoryDto>>> Handle(GetRecentParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;
        var logs = await _parkingLogRepository.GetRecentParkingLogsAsync(limit);

        var dtos = logs.Select(parkingLog =>
            new ParkingLogHistoryDto(
                parkingLog.Vehicle.PlateNumber,
                parkingLog.Vehicle.Brand,
                parkingLog.EntryTime,
                parkingLog.ExitTime)).ToList();

        return Result<IEnumerable<ParkingLogHistoryDto>>.Success(dtos, "Recent parking history retrieved.");
    }
}