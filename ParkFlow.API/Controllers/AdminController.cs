using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount;
using ParkFlow.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace ParkFlow.API.Controllers;

[Route("api/admin")]
[ApiController]
public class AdminController : ControllerBase
{
	private readonly IMediator _mediator;
	private readonly IUserContext _userContext;

	public AdminController(IMediator mediator, IUserContext userContext)
	{
		_mediator = mediator;
		_userContext = userContext;
	}

	/// <summary>
	/// Registers a new admin account. Highly secured.
	/// Can be called using X-Admin-Registration-Key header or by an authenticated SuperAdmin.
	/// </summary>
	[HttpPost("register")]
	public async Task<ActionResult<Result<Guid>>> Register(
		[FromBody] CreateAdminAccountCommand command,
		[FromHeader(Name = "X-Admin-Registration-Key")] string? registrationKey)
	{
		// Resolve current user ID if authenticated
		Guid? currentUserId = null;
		try
		{
			var userId = _userContext.GetUserId();
			if (userId != Guid.Empty)
			{
				currentUserId = userId;
			}
		}
		catch
		{
			// Ignore if user context throws when unauthenticated
		}

		// Bind secure parameters from HTTP context
		var secureCommand = command with 
		{ 
			RegistrationKey = registrationKey ?? command.RegistrationKey,
			CurrentUserId = currentUserId
		};

		var result = await _mediator.Send(secureCommand);
		return this.ToActionResult(result);
	}
}
