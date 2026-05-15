using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

namespace ParkFlow.API.Controllers;

[Route("api/guards")]
[ApiController]
public class GuardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ParkFlow.Persistence.AppDbContext _dbContext;

    public GuardController(IMediator mediator, ParkFlow.Persistence.AppDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create(CreateGuardAccountCommand command)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            await tx.CommitAsync();
            return Ok(result);
        }

        await tx.RollbackAsync();
        return result.ErrorCode == ErrorCode.Conflict
            ? Conflict(result)
            : BadRequest(result);
    }
}