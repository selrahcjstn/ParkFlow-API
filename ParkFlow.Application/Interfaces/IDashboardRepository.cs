using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;

namespace ParkFlow.Application.Interfaces;

public interface IDashboardRepository
{
	Task<int> GetTotalUsersCountAsync();
	Task<decimal> GetTodayRevenueAsync(DateTime? asOfUtc = null);
	Task<int> GetActiveParkingCountAsync();
	Task<DashboardDailyComparison> GetDailyComparisonAsync(DateTime asOfUtc);
	Task<int> GetActiveViolationsCountAsync();
	Task<Dictionary<DateTime, (int CheckIns, int CheckOuts)>> GetActivityOverLast7DaysAsync();
}
