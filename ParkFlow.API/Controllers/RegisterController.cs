using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;
using ParkFlow.Persistence;

namespace ParkFlow.API.Controllers
{
    [Route("api/new-user")]
    [ApiController]
    public class RegisterController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly AppDbContext _dbContext;

        public RegisterController(IMediator mediator, AppDbContext dbContext)
        {
            _mediator = mediator;
            _dbContext = dbContext;
        }

        [HttpPost("register")]
        public async Task<ActionResult<Result<RegisterResultDto>>> Register(RegisterUserAggregateCommand command)
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync();

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                await tx.CommitAsync();
                return Ok(result);
            }

            await tx.RollbackAsync();
            return this.ToActionResult(result);
        }
    }
}
