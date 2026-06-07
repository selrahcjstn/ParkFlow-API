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
		var logs = await _parkingLogRepository.GetActiveParkingLogsAsync(request.ParkingCapacity);
		var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
		var nowUtc = DateTime.UtcNow;

		var activeLogs = logs
			.Where(x => x.EntryTime != default)
			.ToList();

		var overstayCount = 0;

		foreach (var log in activeLogs)
		{
			if (log.EntryMethod == EntryMethod.Manual)
				continue;

			var verifiedCor = corSubmissions.FirstOrDefault(c =>
				c.UserAccountId == log.Vehicle.OwnerId &&
				c.VerificationStatus == CorVerificationStatus.Verified);

			if (verifiedCor == null)
				continue;

			var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
			var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
			var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == philippinesEntry.DayOfWeek);

			if (todaySchedule == null)
				continue;

			var scheduleEndUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(
				philippinesEntry,
				todaySchedule.EndTime);
			var maximumExitTimeUtc = scheduleEndUtc.AddMinutes(30);

			if (nowUtc > maximumExitTimeUtc)
				overstayCount++;
		}

		var manualSessionCount = activeLogs.Count(x => x.EntryMethod == EntryMethod.Manual);

		var response = new SessionCountResponse(
			ActiveSessionCount: activeLogs.Count,
			OverstayCount: overstayCount,
			MaximumCapacity: request.ParkingCapacity,
			ManualSessionCount: manualSessionCount);

		return Result<SessionCountResponse>.Success(response, "Session count retrieved.");
	}
}
