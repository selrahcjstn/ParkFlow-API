using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount;

public class GetSessionCountHandler
	: IRequestHandler<GetSessionCountQuery, Result<SessionCountResponse>>
{
	private readonly IParkingLogRepository _parkingLogRepository;
	private readonly ICorSubmissionRepository _corSubmissionRepository;
	private readonly IParkingScheduleRepository _parkingScheduleRepository;

	public GetSessionCountHandler(
		IParkingLogRepository parkingLogRepository,
		ICorSubmissionRepository corSubmissionRepository,
		IParkingScheduleRepository parkingScheduleRepository)
	{
		_parkingLogRepository = parkingLogRepository;
		_corSubmissionRepository = corSubmissionRepository;
		_parkingScheduleRepository = parkingScheduleRepository;
	}

	public async Task<Result<SessionCountResponse>> Handle(
		GetSessionCountQuery request,
		CancellationToken cancellationToken)
	{
		var sysSettings = SystemSettingsStore.Current;
		var effectiveCapacity = request.ParkingCapacity > 0 
			? request.ParkingCapacity 
			: (sysSettings.TotalCapacity > 0 ? sysSettings.TotalCapacity : 500);

		var logs = await _parkingLogRepository.GetActiveParkingLogsAsync(Math.Max(1000, effectiveCapacity));
		var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
		var nowUtc = DateTime.UtcNow;
		var graceMin = sysSettings.IsGracePeriodEnabled ? sysSettings.GracePeriodMinutes : 0;

		var activeLogs = logs
			.Where(x => x.EntryTime != default)
			.ToList();

		var overstayCount = 0;

		foreach (var log in activeLogs)
		{
			var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);

			if (log.EntryMethod == EntryMethod.Manual)
			{
				var entryMidnightUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(23, 59, 59));
				if (nowUtc > entryMidnightUtc)
				{
					overstayCount++;
				}
				continue;
			}

			var verifiedCor = corSubmissions.FirstOrDefault(c =>
				c.UserAccountId == log.Vehicle.OwnerId &&
				c.VerificationStatus == CorVerificationStatus.Verified);

			if (verifiedCor == null)
				continue;

			var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
			var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == philippinesEntry.DayOfWeek);

			if (todaySchedule == null)
				continue;

			var scheduleEndUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(
				philippinesEntry,
				todaySchedule.EndTime);
			var maximumExitTimeUtc = scheduleEndUtc.AddMinutes(graceMin);

			if (nowUtc > maximumExitTimeUtc)
				overstayCount++;
		}

		var manualSessionCount = activeLogs.Count(x => x.EntryMethod == EntryMethod.Manual);

		var response = new SessionCountResponse(
			ActiveSessionCount: activeLogs.Count,
			OverstayCount: overstayCount,
			MaximumCapacity: effectiveCapacity,
			ManualSessionCount: manualSessionCount);

		return Result<SessionCountResponse>.Success(response, "Session count retrieved.");
	}
}
