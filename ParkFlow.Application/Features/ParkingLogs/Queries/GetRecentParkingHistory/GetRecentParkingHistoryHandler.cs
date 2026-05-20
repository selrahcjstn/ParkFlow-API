using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;

public class GetRecentParkingHistoryHandler : IRequestHandler<GetRecentParkingHistoryQuery, Result<IEnumerable<ParkingLogHistoryDto>>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IViolationRepository _violationRepository;

    public GetRecentParkingHistoryHandler(
        IParkingLogRepository parkingLogRepository,
        IViolationRepository violationRepository)
    {
        _parkingLogRepository = parkingLogRepository;
        _violationRepository = violationRepository;
    }

    public async Task<Result<IEnumerable<ParkingLogHistoryDto>>> Handle(GetRecentParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;
        var logs = await _parkingLogRepository.GetRecentParkingLogsAsync(limit);

        var validLogs = logs.Where(p => p.EntryTime != default).ToList();
        var dtos = new List<ParkingLogHistoryDto>(validLogs.Count);
        var dayCounters = new Dictionary<string, int>();

        foreach (var parkingLog in validLogs)
        {
            var ownerProfile = parkingLog.Vehicle.Owner.UserProfile;
            var violation = await _violationRepository.GetByLogIdAsync(parkingLog.Id);

            var endTime = parkingLog.ExitTime ?? DateTime.UtcNow;
            var totalHours = Math.Max(0, (endTime - parkingLog.EntryTime).TotalHours);
            var today = parkingLog.EntryTime.ToString("yyyyMMdd");
            dayCounters.TryGetValue(today, out var countToday);
            var parkingLogId = $"PL-{today}-{(countToday + 1):D4}";
            dayCounters[today] = countToday + 1;

            dtos.Add(new ParkingLogHistoryDto(
                parkingLogId,
                ownerProfile.FirstName,
                ownerProfile.LastName,
                parkingLog.Vehicle.Owner.PhoneNumber,
                parkingLog.Vehicle.PlateNumber,
                parkingLog.Vehicle.Brand,
                parkingLog.Vehicle.VehicleType.ToString(),
                parkingLog.EntryTime.ToString("O"),
                parkingLog.ExitTime?.ToString("O"),
                violation?.SettlementStatus.ToString() ?? "None",
                violation?.PenaltyFee ?? 0m,
                parkingLog.ExitTime?.ToString("O") ?? string.Empty,
                totalHours.ToString("0.##")));
        }

        return Result<IEnumerable<ParkingLogHistoryDto>>.Success(dtos, "Recent parking history retrieved.");
    }
}
