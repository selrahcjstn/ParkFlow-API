using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount;
using ParkFlow.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ParkFlow.API.Controllers;

[Route("api/admin")]
[ApiController]
public class AdminController : ControllerBase
{
	private readonly IMediator _mediator;
	private readonly IUserContext _userContext;
	private readonly IConfiguration _configuration;

	public AdminController(IMediator mediator, IUserContext userContext, IConfiguration configuration)
	{
		_mediator = mediator;
		_userContext = userContext;
		_configuration = configuration;
	}

	/// <summary>
	/// Registers a new admin account. Highly secured.
	/// Can be called using X-Admin-Registration-Key header or by an authenticated SuperAdmin.
	/// </summary>
	[AllowAnonymous]
	[HttpPost("register")]
	[HttpPost("")]
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

		if (!currentUserId.HasValue || currentUserId == Guid.Empty)
		{
			var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst("sub")?.Value
				?? User.FindFirst("user_id")?.Value;

			if (Guid.TryParse(sub, out var parsedGuid) && parsedGuid != Guid.Empty)
			{
				currentUserId = parsedGuid;
			}
		}

		var isSuperAdminClaim = User.IsInRole("SuperAdmin")
			|| User.HasClaim("role", "SuperAdmin")
			|| User.HasClaim("profile_type", "superadmin")
			|| string.Equals(User.FindFirst(ClaimTypes.Email)?.Value, "superadmin@parkflow.com", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(User.FindFirst("email")?.Value, "superadmin@parkflow.com", StringComparison.OrdinalIgnoreCase);

		var effectiveKey = registrationKey ?? command.RegistrationKey;
		if (string.IsNullOrWhiteSpace(effectiveKey) && isSuperAdminClaim)
		{
			effectiveKey = _configuration["AdminSettings:RegistrationKey"];
		}

		// Bind secure parameters from HTTP context
		var secureCommand = command with 
		{ 
			RegistrationKey = effectiveKey,
			CurrentUserId = currentUserId
		};

		var result = await _mediator.Send(secureCommand);
		return this.ToActionResult(result);
	}
}
