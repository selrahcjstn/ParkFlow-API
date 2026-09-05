using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.Commands.CreateFeedback;
using ParkFlow.Application.Features.Feedbacks.Commands.ReplyToFeedback;
using ParkFlow.Application.Features.Feedbacks.Commands.UpdateFeedbackStatus;
using ParkFlow.Application.Features.Feedbacks.Queries.GetFeedbacks;
using ParkFlow.Application.Features.Feedbacks.Queries.GetMyFeedbacks;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.API.Controllers;

public record CreateFeedbackRequest(string Category, int Rating, string Description, string? AttachmentUrl);
public record UpdateFeedbackStatusRequest(FeedbackStatus Status, string? AdminNotes);
public record ReplyToFeedbackRequest(string ReplyMessage, decimal? InvoiceAmount = null, string? InvoiceDescription = null, bool MarkResolved = false);

[Route("api/feedbacks")]
[ApiController]
public class FeedbackController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public FeedbackController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// Submits user feedback or suggestion.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<FeedbackDto>>> SubmitFeedback([FromBody] CreateFeedbackRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<FeedbackDto>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new CreateFeedbackCommand(
            userId,
            request.Category,
            request.Rating,
            request.Description,
            request.AttachmentUrl
        );

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Returns current logged-in user's submitted feedback history and admin replies.
    /// </summary>
    [HttpGet("my-feedbacks")]
    [Authorize]
    public async Task<ActionResult<Result<IEnumerable<FeedbackDto>>>> GetMyFeedbacks()
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<IEnumerable<FeedbackDto>>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new GetMyFeedbacksQuery(userId));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Returns all feedback items for Admin management.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<Result<IEnumerable<FeedbackDto>>>> GetAllFeedbacks(
        [FromQuery] string? category = null,
        [FromQuery] int? status = null)
    {
        var result = await _mediator.Send(new GetFeedbacksQuery(category, status));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Sends an admin reply/answer (and optional invoice) to feedback sender.
    /// </summary>
    [HttpPost("{id:guid}/reply")]
    [Authorize]
    public async Task<ActionResult<Result<FeedbackDto>>> ReplyToFeedback(
        [FromRoute] Guid id,
        [FromBody] ReplyToFeedbackRequest request)
    {
        var command = new ReplyToFeedbackCommand(
            id,
            request.ReplyMessage,
            request.InvoiceAmount,
            request.InvoiceDescription,
            request.MarkResolved
        );

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Updates the status or admin notes of a feedback item.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize]
    public async Task<ActionResult<Result<FeedbackDto>>> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateFeedbackStatusRequest request)
    {
        var command = new UpdateFeedbackStatusCommand(id, request.Status, request.AdminNotes);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
