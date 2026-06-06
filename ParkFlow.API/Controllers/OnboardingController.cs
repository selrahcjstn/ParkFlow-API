using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Features.Onboarding.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

[Route("api/onboarding")]
[ApiController]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public OnboardingController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpPatch("profile")]
    public async Task<ActionResult<Result<Guid>>> UpdateProfile(OnboardingProfileRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateOnboardingProfileCommand(
            userId,
            request.PhoneNumber,
            request.FirstName,
            request.LastName,
            request.MiddleName,
            request.ProfilePictureUrl);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPatch("student")]
    public async Task<ActionResult<Result<Guid>>> UpdateStudent(OnboardingStudentRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateOnboardingStudentCommand(
            userId,
            request.StudentNumber,
            request.Course,
            request.Section,
            request.YearLevel);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPatch("personnel")]
    public async Task<ActionResult<Result<Guid>>> UpdatePersonnel(OnboardingPersonnelRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateOnboardingPersonnelCommand(
            userId,
            request.IdCardNumber,
            request.Department);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPatch("vehicle")]
    public async Task<ActionResult<Result<Guid>>> UpdateVehicle(OnboardingVehicleRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateOnboardingVehicleCommand(
            userId,
            request.PlateNumber,
            request.Brand,
            request.VehicleType);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPatch("schedule")]
    public async Task<ActionResult<Result<Guid>>> UpdateSchedule(OnboardingScheduleRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var items = request.Items.Select(i => new ScheduleItem(i.DayOfWeek, i.StartTime, i.EndTime)).ToList();
        var command = new UpdateOnboardingScheduleCommand(userId, items);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPatch("cor")]
    public async Task<ActionResult<Result<Guid>>> UpdateCor(OnboardingCorRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateOnboardingCorCommand(
            userId,
            request.AcademicTerm,
            request.CorDocumentUrl);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
