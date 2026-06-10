using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;
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
}
