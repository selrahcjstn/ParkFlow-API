using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Features.Users.Commands.CreateUserAccount;
using ParkFlow.Application.Features.Users.Commands.UpdateUserAccount;

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

        [HttpPost]
        public async Task<ActionResult<Guid>> Create(CreateUserAccountCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<Guid>> Update(Guid id, UpdateUserAccountCommand command)
        {
            var updatedCommand = command with { Id = id };
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}