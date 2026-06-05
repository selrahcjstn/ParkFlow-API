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
    private readonly IViolationService _violationService;

    public GetActiveParkingSessionHandler(
        IParkingLogRepository parkingLogRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IViolationService violationService)
    {
        _parkingLogRepository = parkingLogRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _violationService = violationService;
    }

    public async Task<Result<IEnumerable<GetActiveParkingSessionResponse>>> Handle(
        GetActiveParkingSessionQuery request,
        CancellationToken cancellationToken)
    {
        var logs = await _parkingLogRepository
            .GetActiveParkingLogsAsync(request.ParkingCapacity);

        var corSubmissions = await _corSubmissionRepository
            .ListCorSubmissionsAsync();

        var activeLogs = logs
            .Where(x => x.EntryTime != default)
            .ToList();

        var dtos = new List<GetActiveParkingSessionResponse>();

        foreach (var log in activeLogs)
        {
            var nowUtc = DateTime.UtcNow;
            var overstayHours = 0d;
            var amount = 0m;

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

            if (maximumExitTimeUtc.HasValue && nowUtc > maximumExitTimeUtc.Value)
            {
                var overstayDuration = nowUtc - maximumExitTimeUtc.Value;
                overstayHours = overstayDuration.TotalHours;
                amount = _violationService.CalculatePenalty(overstayDuration);
            }

            var ownerProfile = log.Vehicle.Owner?.UserProfile;
            var ownerPhoneNumber = ownerProfile?.UserAccount?.PhoneNumber;

            if (ownerProfile == null || string.IsNullOrWhiteSpace(ownerPhoneNumber))
            {
                continue;
            }

            var totalHours = Math.Max(0, (nowUtc - log.EntryTime).TotalHours);

            dtos.Add(new GetActiveParkingSessionResponse(
                FirstName: ownerProfile.FirstName,
                LastName: ownerProfile.LastName,
                MiddleName: ownerProfile.MiddleName,
                PhoneNumber: ownerPhoneNumber,

                Status: log.Status.ToString(),
                PlateNumber: log.Vehicle.PlateNumber,
                Brand: log.Vehicle.Brand,
                VehicleType: log.Vehicle.VehicleType.ToString(),

                EntryTime: log.EntryTime,
                MaximumExitTime: maximumExitTimeUtc ?? default,
                OverstayHours: overstayHours,
                Amount: amount,

                TotalParkingHours: $"{totalHours:F2} hours"
            ));
        }

        return Result<IEnumerable<GetActiveParkingSessionResponse>>
            .Success(dtos, "Active parking sessions retrieved.");
    }
}