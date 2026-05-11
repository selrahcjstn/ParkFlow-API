using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Schedules.Command;

public class CreateParkingScheduleHandler : IRequestHandler<CreateParkingScheduleCommand, Result<Guid>>
{
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IValidator<CreateParkingScheduleCommand> _validator;

    public CreateParkingScheduleHandler(
        IParkingScheduleRepository parkingScheduleRepository,
        IValidator<CreateParkingScheduleCommand> validator)
    {
        _parkingScheduleRepository = parkingScheduleRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(CreateParkingScheduleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var parkingSchedule = new ParkingSchedule(request.SubmissionId, request.DayOfWeek, request.StartTime, request.EndTime);

        await _parkingScheduleRepository.AddAsync(parkingSchedule);

        return Result<Guid>.Success(parkingSchedule.Id, "Parking schedule created.");
    }
}
