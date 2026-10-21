using MediatR;
using ParkFlow.Application.Common;
using System.Collections.Generic;

namespace ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;

public record GetDashboardSummaryQuery(int ParkingCapacity = 150) : IRequest<Result<DashboardSummaryResponse>>;

public record DashboardSummaryResponse(
	int TotalUsers,
	int ActiveParking,
	int MaxCapacity,
	decimal TodayRevenue,
	int ViolationsCount,
	List<ParkingActivityDto> ActivityOverLast7Days,
	DashboardTrendsDto? Trends = null,
    int CarCount = 0, int MotorcycleCount = 0, int CarCapacity = 0, int MotorcycleCapacity = 0
);

public record DashboardDailyComparison(int RegisteredClients, decimal? Revenue, int? PendingCharges, DateTime Date);

public record DashboardTrendsDto(
	decimal? RegisteredClientsPercent,
	decimal? RevenuePercent,
	decimal? PendingChargesPercent,
	DateTime ComparisonDate
);

public record ParkingActivityDto(
	string Day,
	int CheckIns,
	int CheckOuts
);
