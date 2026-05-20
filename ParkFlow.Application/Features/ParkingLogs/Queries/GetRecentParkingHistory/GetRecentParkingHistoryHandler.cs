using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;

public class GetRecentParkingHistoryHandler : IRequestHandler<GetRecentParkingHistoryQuery, Result<IEnumerable<ParkingLogHistoryDto>>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IViolationRepository _violationRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;

    public GetRecentParkingHistoryHandler(
        IParkingLogRepository parkingLogRepository,
        IViolationRepository violationRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository)
    {
        _parkingLogRepository = parkingLogRepository;
        _violationRepository = violationRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
    }

    public async Task<Result<IEnumerable<ParkingLogHistoryDto>>> Handle(GetRecentParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;
        var logs = await _parkingLogRepository.GetRecentParkingLogsAsync(limit);

        var validLogs = logs.Where(p => p.EntryTime != default).ToList();
        var dtos = new List<ParkingLogHistoryDto>(validLogs.Count);
        var mustExitByCache = new Dictionary<(Guid OwnerId, DateOnly EntryDate), DateTime?>();
        foreach (var parkingLog in validLogs)
        {
            var ownerProfile = parkingLog.Vehicle.Owner.UserProfile;
            var violation = await _violationRepository.GetByLogIdAsync(parkingLog.Id);

            var effectiveEntryTime = violation?.RecordedEntryTime ?? parkingLog.EntryTime;
            var effectiveExitTime = violation?.RecordedExitTime ?? parkingLog.ExitTime;
            var effectiveStatus = violation?.RecordedStatus.ToString() ?? parkingLog.Status.ToString();
            var endTime = effectiveExitTime ?? DateTime.UtcNow;
            var totalHours = Math.Max(0, (endTime - effectiveEntryTime).TotalHours);
            var parkingLogId = ParkingLogIdHelper.GenerateHistoryId(parkingLog.EntryTime);
            var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(effectiveEntryTime);
            var cacheKey = (parkingLog.Vehicle.OwnerId, DateOnly.FromDateTime(philippinesEntry));

            if (!mustExitByCache.TryGetValue(cacheKey, out var mustExitByUtc))
            {
                mustExitByUtc = await ResolveMustExitByUtcAsync(parkingLog.Vehicle.OwnerId, philippinesEntry);
                mustExitByCache[cacheKey] = mustExitByUtc;
            }

            dtos.Add(new ParkingLogHistoryDto(
                parkingLogId,
                ownerProfile.FirstName,
                ownerProfile.LastName,
                parkingLog.Vehicle.Owner.PhoneNumber,
                effectiveStatus,
                parkingLog.Vehicle.PlateNumber,
                parkingLog.Vehicle.Brand,
                parkingLog.Vehicle.VehicleType.ToString(),
                effectiveEntryTime.ToString("O"),
                effectiveExitTime?.ToString("O"),
                violation?.SettlementStatus.ToString() ?? "None",
                violation?.PenaltyFee ?? 0m,
                mustExitByUtc?.ToString("O") ?? string.Empty,
                totalHours.ToString("0.##")));
        }

        return Result<IEnumerable<ParkingLogHistoryDto>>.Success(dtos, "Recent parking history retrieved.");
    }

    private async Task<DateTime?> ResolveMustExitByUtcAsync(Guid ownerId, DateTime philippinesEntry)
    {
        var submissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var verifiedCor = submissions
            .FirstOrDefault(c => c.UserAccountId == ownerId && c.VerificationStatus == CorVerificationStatus.Verified);

        if (verifiedCor == null)
            return null;

        var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
        var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == philippinesEntry.DayOfWeek);

        if (todaySchedule == null)
            return null;

        return ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, todaySchedule.EndTime);
    }
}
