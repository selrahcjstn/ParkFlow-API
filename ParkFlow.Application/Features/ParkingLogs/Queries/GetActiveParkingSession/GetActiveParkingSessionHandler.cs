using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;

public class GetActiveParkingSessionHandler
    : IRequestHandler<GetActiveParkingSessionQuery, Result<GetActiveParkingSessionResult>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public GetActiveParkingSessionHandler(
        IParkingLogRepository parkingLogRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository)
    {
        _parkingLogRepository = parkingLogRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
    }

    public async Task<Result<GetActiveParkingSessionResult>> Handle(
        GetActiveParkingSessionQuery request,
        CancellationToken cancellationToken)
    {
        var (logs, totalCount) = await _parkingLogRepository.GetTodaysParkingLogsAsync(request.ParkingCapacity);

        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        var dtos = logs
            .Where(x => x.EntryTime != default && x.ExitTime == null && x.Status == ParkingStatus.Parked)
            .Select(log =>
            {
                var nowUtc = DateTime.UtcNow;

                var verifiedCor = corSubmissions.FirstOrDefault(c =>
                    c.UserAccountId == log.Vehicle.OwnerId &&
                    c.VerificationStatus == CorVerificationStatus.Verified);

                DateTime? maximumExitTimeUtc = null;

                if (verifiedCor != null)
                {
                    var schedules = _parkingScheduleRepository
                        .GetBySubmissionIdAsync(verifiedCor.Id)
                        .Result;

                    var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);

                    var todaySchedule = schedules?
                        .FirstOrDefault(s => s.DayOfWeek == philippinesEntry.DayOfWeek);

                    if (todaySchedule != null)
                    {
                        var scheduleEndUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, todaySchedule.EndTime);
                        maximumExitTimeUtc = scheduleEndUtc.AddMinutes(30);
                    }
                }

                var totalHours = Math.Max(0, (nowUtc - log.EntryTime).TotalHours);

                return new GetActiveParkingSessionResponse(
                    FirstName: log.Vehicle.Owner.UserProfile.FirstName,
                    LastName: log.Vehicle.Owner.UserProfile.LastName,
                    PhoneNumber: log.Vehicle.Owner.UserProfile.UserAccount.PhoneNumber,

                    Status: log.Status.ToString(),
                    PlateNumber: log.Vehicle.PlateNumber,
                    Brand: log.Vehicle.Brand,

                    EntryTime: log.EntryTime,

                    MaximumExitTime: maximumExitTimeUtc ?? default,

                    TotalParkingHours: $"{totalHours:F2} hours"
                );
            })
            .ToList();

        var result = new GetActiveParkingSessionResult(dtos, TotalCount: totalCount, ParkingCapacity: request.ParkingCapacity);

        return Result<GetActiveParkingSessionResult>
            .Success(result, "Active parking sessions retrieved.");
    }
}