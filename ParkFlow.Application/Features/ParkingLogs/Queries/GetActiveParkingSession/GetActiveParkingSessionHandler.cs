// Handler
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;

public class GetActiveParkingSessionHandler
    : IRequestHandler<
        GetActiveParkingSessionQuery,
        Result<IEnumerable<GetActiveParkingSessionResponse>>>
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

    public async Task<Result<IEnumerable<GetActiveParkingSessionResponse>>> Handle(
        GetActiveParkingSessionQuery request,
        CancellationToken cancellationToken)
    {
        var logs = await _parkingLogRepository
            .GetTodaysParkingLogsAsync(request.ParkingCapacity);

        var corSubmissions = await _corSubmissionRepository
            .ListCorSubmissionsAsync();

        var activeLogs = logs
            .Where(x =>
                x.EntryTime != default &&
                x.ExitTime == null &&
                x.Status == ParkingStatus.Parked)
            .ToList();

        var dtos = new List<GetActiveParkingSessionResponse>();

        foreach (var log in activeLogs)
        {
            var nowUtc = DateTime.UtcNow;

            var verifiedCor = corSubmissions.FirstOrDefault(c =>
                c.UserAccountId == log.Vehicle.OwnerId &&
                c.VerificationStatus == CorVerificationStatus.Verified);

            DateTime? maximumExitTimeUtc = null;

            if (verifiedCor != null)
            {
                var schedules = await _parkingScheduleRepository
                    .GetBySubmissionIdAsync(verifiedCor.Id);

                var philippinesEntry =
                    ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);

                var todaySchedule = schedules?
                    .FirstOrDefault(s =>
                        s.DayOfWeek == philippinesEntry.DayOfWeek);

                if (todaySchedule != null)
                {
                    var scheduleEndUtc =
                        ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(
                            philippinesEntry,
                            todaySchedule.EndTime);

                    maximumExitTimeUtc = scheduleEndUtc.AddMinutes(30);
                }
            }

            var totalHours = Math.Max(
                0,
                (nowUtc - log.EntryTime).TotalHours);

            dtos.Add(new GetActiveParkingSessionResponse(
                FirstName: log.Vehicle.Owner.UserProfile.FirstName,
                LastName: log.Vehicle.Owner.UserProfile.LastName,
                PhoneNumber: log.Vehicle.Owner.UserProfile.UserAccount.PhoneNumber,

                Status: log.Status.ToString(),
                PlateNumber: log.Vehicle.PlateNumber,
                Brand: log.Vehicle.Brand,
                VehicleType: log.Vehicle.VehicleType.ToString(),

                EntryTime: log.EntryTime,
                MaximumExitTime: maximumExitTimeUtc ?? default,

                TotalParkingHours: $"{totalHours:F2} hours"
            ));
        }

        return Result<IEnumerable<GetActiveParkingSessionResponse>>
            .Success(dtos, "Active parking sessions retrieved.");
    }
}