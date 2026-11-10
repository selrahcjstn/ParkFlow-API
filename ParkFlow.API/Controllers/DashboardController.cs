using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.API.Controllers;

[Route("api/dashboard")]
[ApiController]
[Authorize]
public class DashboardController : ControllerBase
{
	private readonly IMediator _mediator;

	public DashboardController(IMediator mediator)
	{
		_mediator = mediator;
	}

	/// <summary>
	/// Gets real-time dashboard stats and parking logs activity aggregates.
	/// </summary>
	[HttpGet("summary")]
	public async Task<ActionResult<Result<DashboardSummaryResponse>>> GetSummary([FromQuery] int parkingCapacity = 150)
	{
		var result = await _mediator.Send(new GetDashboardSummaryQuery(parkingCapacity));
		return this.ToActionResult(result);
	}

	/// <summary>Account, vehicle and approval counts plus today's operations and the latest transactions.</summary>
	[Authorize(Policy = "AdminOrSuperAdmin")]
	[HttpGet("overview")]
	public async Task<ActionResult<Result<DashboardOverviewResponse>>> GetOverview(CancellationToken cancellationToken)
		=> this.ToActionResult(await _mediator.Send(new GetDashboardOverviewQuery(), cancellationToken));

	/// <summary>Entries/exits time series and peak hours. <c>range</c> is today, 7d or 30d (Philippine time).</summary>
	[Authorize(Policy = "AdminOrSuperAdmin")]
	[HttpGet("activity")]
	public async Task<ActionResult<Result<DashboardActivityResponse>>> GetActivity([FromQuery] string range = "7d", CancellationToken cancellationToken = default)
	{
		ActivityRange? parsed = range?.Trim().ToLowerInvariant() switch
		{
			"today" => ActivityRange.Today,
			"7d" => ActivityRange.Last7Days,
			"30d" => ActivityRange.Last30Days,
			_ => null,
		};
		if (parsed == null) return BadRequest("Range must be today, 7d or 30d.");
		return this.ToActionResult(await _mediator.Send(new GetDashboardActivityQuery(parsed.Value), cancellationToken));
	}
}
