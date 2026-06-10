using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ParkFlow.Persistence.Repositories;

public class DashboardRepository : IDashboardRepository
{
	private readonly AppDbContext _context;

	public DashboardRepository(AppDbContext context)
	{
		_context = context;
	}

	public async Task<int> GetTotalUsersCountAsync()
	{
		return await _context.UserAccounts.CountAsync();
	}

	public async Task<decimal> GetTodayRevenueAsync()
	{
		var today = DateTime.UtcNow.Date;
		// Sum PenaltyFee of all violations marked as Settled today
		return await _context.Violations
			.Where(v => v.SettlementStatus == SettlementStatus.Settled && v.UpdatedAt != null && v.UpdatedAt.Value.Date == today)
			.SumAsync(v => v.PenaltyFee);
	}

	public async Task<int> GetActiveViolationsCountAsync()
	{
		// Unsettled/Pending violations count
		return await _context.Violations
			.Where(v => v.SettlementStatus == SettlementStatus.Pending)
			.CountAsync();
	}

	public async Task<Dictionary<DateTime, (int CheckIns, int CheckOuts)>> GetActivityOverLast7DaysAsync()
	{
		var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
		
		// Load recent parking logs of the last 7 days
		var logs = await _context.ParkingLogs
			.Where(p => p.EntryTime >= sevenDaysAgo)
			.ToListAsync();

		var result = new Dictionary<DateTime, (int CheckIns, int CheckOuts)>();
		for (int i = 0; i < 7; i++)
		{
			var date = sevenDaysAgo.AddDays(i);
			var checkIns = logs.Count(l => l.EntryTime.Date == date);
			var fillCheckOuts = logs.Count(l => l.ExitTime.HasValue && l.ExitTime.Value.Date == date);
			result[date] = (checkIns, fillCheckOuts);
		}

		return result;
	}
}
