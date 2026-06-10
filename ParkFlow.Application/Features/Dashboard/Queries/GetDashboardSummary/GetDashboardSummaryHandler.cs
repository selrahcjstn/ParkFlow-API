using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;

public class GetDashboardSummaryHandler : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryResponse>>
{
	private readonly IDashboardRepository _dashboardRepository;
	private readonly IParkingLogRepository _parkingLogRepository;

	public GetDashboardSummaryHandler(
		IDashboardRepository dashboardRepository,
		IParkingLogRepository parkingLogRepository)
	{
		_dashboardRepository = dashboardRepository;
		_parkingLogRepository = parkingLogRepository;
	}

	public async Task<Result<DashboardSummaryResponse>> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
	{
		try
		{
			var totalUsers = await _dashboardRepository.GetTotalUsersCountAsync();
			var activeLogs = await _parkingLogRepository.GetActiveParkingLogsAsync(request.ParkingCapacity);
			var todayRevenue = await _dashboardRepository.GetTodayRevenueAsync();
			var violationsCount = await _dashboardRepository.GetActiveViolationsCountAsync();
			
			var activityDict = await _dashboardRepository.GetActivityOverLast7DaysAsync();
			
			var activityList = activityDict
				.OrderBy(kvp => kvp.Key)
				.Select(kvp => new ParkingActivityDto(
					Day: kvp.Key.ToString("ddd"), // Mon, Tue, Wed...
					CheckIns: kvp.Value.CheckIns,
					CheckOuts: kvp.Value.CheckOuts
				))
				.ToList();

			var response = new DashboardSummaryResponse(
				TotalUsers: totalUsers,
				ActiveParking: activeLogs.Count,
				MaxCapacity: request.ParkingCapacity,
				TodayRevenue: todayRevenue,
				ViolationsCount: violationsCount,
				ActivityOverLast7Days: activityList
			);

			return Result<DashboardSummaryResponse>.Success(response, "Dashboard summary retrieved successfully.");
		}
		catch (Exception ex)
		{
			return Result<DashboardSummaryResponse>.Failure($"Failed to retrieve dashboard summary: {ex.Message}", ErrorCode.ServerError);
		}
	}
}
