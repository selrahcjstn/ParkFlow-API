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
    private readonly ICacheService? _cacheService;
    private readonly IParkingLogRepository? _parkingLogRepository;

    public GetDashboardSummaryHandler(
        IDashboardRepository dashboardRepository,
        ICacheService? cacheService = null, IParkingLogRepository? parkingLogRepository = null)
    {
        _dashboardRepository = dashboardRepository;
        _cacheService = cacheService;
        _parkingLogRepository = parkingLogRepository;
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

            async Task<DashboardSummaryResponse> LoadSummaryAsync()
            {
                var asOfUtc = DateTime.UtcNow;
                var totalUsers = await _dashboardRepository.GetTotalUsersCountAsync();
                var activeParking = await _dashboardRepository.GetActiveParkingCountAsync();
                var todayRevenue = await _dashboardRepository.GetTodayRevenueAsync(asOfUtc);
                var violationsCount = await _dashboardRepository.GetActiveViolationsCountAsync();
                var previous = await _dashboardRepository.GetDailyComparisonAsync(asOfUtc);

                var activityDict = await _dashboardRepository.GetActivityOverLast7DaysAsync();

                var activityList = activityDict
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => new ParkingActivityDto(
                        Day: kvp.Key.ToString("ddd"), // Mon, Tue, Wed...
                        CheckIns: kvp.Value.CheckIns,
                        CheckOuts: kvp.Value.CheckOuts
                    ))
                    .ToList();

                var occupancy = _parkingLogRepository == null ? (Cars: 0, Motorcycles: 0) : await _parkingLogRepository.GetOccupancyAsync();
                var response = new DashboardSummaryResponse(
                    TotalUsers: totalUsers,
                    ActiveParking: activeParking,
                    MaxCapacity: effectiveCapacity,
                    TodayRevenue: todayRevenue,
                    ViolationsCount: violationsCount,
                    ActivityOverLast7Days: activityList,
                    Trends: new DashboardTrendsDto(
                        CalculatePercentageChange(totalUsers, previous.RegisteredClients),
                        previous.Revenue.HasValue
                            ? CalculatePercentageChange(todayRevenue, previous.Revenue.Value)
                            : null,
                        previous.PendingCharges.HasValue
                            ? CalculatePercentageChange(violationsCount, previous.PendingCharges.Value)
                            : null,
                        previous.Date)
                );

                return response with { CarCount = occupancy.Cars, MotorcycleCount = occupancy.Motorcycles,
                    CarCapacity = sysSettings.CarParking.TotalCapacity, MotorcycleCapacity = sysSettings.MotorcycleParking.TotalCapacity };
            }
            var response = _cacheService != null
                ? await _cacheService.GetOrCreateAsync(cacheKey, LoadSummaryAsync, TimeSpan.FromSeconds(30), cancellationToken)
                : await LoadSummaryAsync();

            return Result<DashboardSummaryResponse>.Success(response, "Dashboard summary retrieved successfully.");
        }
        catch (Exception ex)
        {
            return Result<DashboardSummaryResponse>.Failure($"Failed to retrieve dashboard summary: {ex.Message}", ErrorCode.ServerError);
        }
    }

    public static decimal? CalculatePercentageChange(decimal current, decimal previous)
    {
        if (previous == 0m) return current == 0m ? 0m : null;
        return Math.Round((current - previous) / previous * 100m, 1, MidpointRounding.AwayFromZero);
    }
}
