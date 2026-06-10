using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParkFlow.Application.Interfaces;

public interface IDashboardRepository
{
	Task<int> GetTotalUsersCountAsync();
	Task<decimal> GetTodayRevenueAsync();
	Task<int> GetActiveViolationsCountAsync();
	Task<Dictionary<DateTime, (int CheckIns, int CheckOuts)>> GetActivityOverLast7DaysAsync();
}
