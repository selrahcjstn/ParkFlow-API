using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Features.Users.Commands.CreateUserAccount;
using ParkFlow.Application.Features.Users.Commands.LoginUserAccount;
using ParkFlow.Application.Features.Users.Commands.UpdateUserAccount;
using ParkFlow.Application.Features.Users.DTOs;

namespace ParkFlow.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserAccountController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserAccountController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("register")]
        public async Task<ActionResult<Guid>> Create(CreateUserAccountCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

       [HttpPatch("{id}")]
        public async Task<ActionResult<Guid>> Update(Guid id, UpdateUserAccountRequest request)
        {
            var command = new UpdateUserAccountCommand(
                id,
                request.Email,
                request.PhoneNumber,
                request.Role
            );

            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<ActionResult<Guid>> Login(LoginRequestDTO request)
        {
           var command = new LoginUserAccountCommand(
                request.Email,
                request.Password
            );

            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}