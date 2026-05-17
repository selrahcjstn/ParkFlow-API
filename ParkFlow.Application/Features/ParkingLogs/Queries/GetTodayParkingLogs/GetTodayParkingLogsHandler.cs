using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetTodayParkingLogs;

public class GetTodayParkingLogsHandler : IRequestHandler<GetTodayParkingLogsQuery, Result<ParkingLogsTodayResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetTodayParkingLogsHandler(IParkingLogRepository parkingLogRepository)
    {
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<ParkingLogsTodayResponse>> Handle(GetTodayParkingLogsQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 100 : request.Limit;
        var (items, totalCount) = await _parkingLogRepository.GetTodaysParkingLogsAsync(limit);

        var dtos = items.Select(MapToDto);
        var response = new ParkingLogsTodayResponse(totalCount, limit, dtos);

        return Result<ParkingLogsTodayResponse>.Success(response, "Today's parking logs retrieved.");
    }

    private static ParkingLogActivityDto MapToDto(ParkFlow.Domain.Entities.ParkingLog parkingLog)
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
    }
}