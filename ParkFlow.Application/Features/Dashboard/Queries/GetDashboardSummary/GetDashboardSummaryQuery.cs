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
	List<ParkingActivityDto> ActivityOverLast7Days
);

public record ParkingActivityDto(
	string Day,
	int CheckIns,
	int CheckOuts
);
