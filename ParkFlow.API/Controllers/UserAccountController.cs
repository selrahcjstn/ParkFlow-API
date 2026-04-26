using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Features.Users.Commands.CreateUserAccount;

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
    }
}