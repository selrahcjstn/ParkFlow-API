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
	private readonly ICacheService? _cacheService;

	public GetDashboardSummaryHandler(
		IDashboardRepository dashboardRepository,
		IParkingLogRepository parkingLogRepository,
		ICacheService? cacheService = null)
	{
		_dashboardRepository = dashboardRepository;
		_parkingLogRepository = parkingLogRepository;
		_cacheService = cacheService;
	}

	public async Task<Result<DashboardSummaryResponse>> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
	{
		try
		{
			var sysSettings = SystemSettingsStore.Current;
			var effectiveCapacity = sysSettings.TotalCapacity > 0
				? sysSettings.TotalCapacity
				: (request.ParkingCapacity > 0 ? request.ParkingCapacity : 500);

			var cacheKey = CacheKeys.DashboardSummary(effectiveCapacity);

			if (_cacheService != null)
			{
				var cached = await _cacheService.GetAsync<DashboardSummaryResponse>(cacheKey, cancellationToken);
				if (cached != null)
				{
					return Result<DashboardSummaryResponse>.Success(cached, "Dashboard summary retrieved successfully.");
				}
			}

			var totalUsers = await _dashboardRepository.GetTotalUsersCountAsync();
			var activeLogs = await _parkingLogRepository.GetActiveParkingLogsAsync(Math.Max(1000, effectiveCapacity));
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
				MaxCapacity: effectiveCapacity,
				TodayRevenue: todayRevenue,
				ViolationsCount: violationsCount,
				ActivityOverLast7Days: activityList
			);

			if (_cacheService != null)
			{
				await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromSeconds(30), cancellationToken);
			}

			return Result<DashboardSummaryResponse>.Success(response, "Dashboard summary retrieved successfully.");
		}
		catch (Exception ex)
		{
			return Result<DashboardSummaryResponse>.Failure($"Failed to retrieve dashboard summary: {ex.Message}", ErrorCode.ServerError);
		}
	}
}
