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
		var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);

		var activeLogs = logs
			.Where(x => x.EntryTime != default)
			.ToList();

		var overstayCount = 0;

		foreach (var log in activeLogs)
		{
			var verifiedCor = corSubmissions.FirstOrDefault(c =>
				c.UserAccountId == log.Vehicle.OwnerId &&
				c.VerificationStatus == CorVerificationStatus.Verified);

			if (verifiedCor == null)
				continue;

			var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
			var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == philippinesNow.DayOfWeek);

			if (todaySchedule == null)
				continue;

			if (philippinesNow.TimeOfDay > todaySchedule.EndTime)
				overstayCount++;
		}

		var response = new SessionCountResponse(
			ActiveSessionCount: activeLogs.Count,
			OverstayCount: overstayCount,
			MaximumCapacity: request.ParkingCapacity);

		return Result<SessionCountResponse>.Success(response, "Session count retrieved.");
	}
}
