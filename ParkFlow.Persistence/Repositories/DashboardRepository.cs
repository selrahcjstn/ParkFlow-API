using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;
using ParkFlow.Application.Features.ParkingLogs.Services;
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
		// This metric is displayed as Registered Clients, not system accounts.
		return await ClientAccounts.CountAsync();
	}

	private IQueryable<UserAccount> ClientAccounts => _context.UserAccounts.Where(user =>
			user.UserProfile == null ||
			(user.UserProfile.Admin == null && user.UserProfile.Guard == null));

	public async Task<int> GetActiveParkingCountAsync()
	{
		var registered = await _context.ParkingLogs.CountAsync(log => log.Status == ParkingStatus.Parked && log.ExitTime == null);
		var visitors = await _context.VisitSessions.CountAsync(session => session.Status == VisitSessionStatus.Inside && session.ExitTime == null);
		return registered + visitors;
	}

	public async Task<DashboardDailyComparison> GetDailyComparisonAsync(DateTime asOfUtc)
	{
		var today = ParkingTimeHelper.ConvertUtcToPhilippinesTime(asOfUtc).Date;
		var startUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(today, TimeSpan.Zero);
		// Cumulative clients at yesterday's close, based on surviving account records.
		var previousClients = await ClientAccounts.CountAsync(user => user.CreatedAt < startUtc);
		return new DashboardDailyComparison(previousClients, today.AddDays(-1));
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
