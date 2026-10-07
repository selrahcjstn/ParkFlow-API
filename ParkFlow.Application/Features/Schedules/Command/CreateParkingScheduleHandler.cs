using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Schedules.Command;

public class CreateParkingScheduleHandler : IRequestHandler<CreateParkingScheduleCommand, Result<Guid>>
{
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IValidator<CreateParkingScheduleCommand> _validator;

    private readonly ICacheService? _cacheService;

    public CreateParkingScheduleHandler(
        IParkingScheduleRepository parkingScheduleRepository,
        IValidator<CreateParkingScheduleCommand> validator,
        ICacheService? cacheService = null)
    {
        _parkingScheduleRepository = parkingScheduleRepository;
        _validator = validator;
        _cacheService = cacheService;
    }

    public async Task<Result<Guid>> Handle(CreateParkingScheduleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var schedules = new List<ParkingSchedule>();

        foreach (var item in request.Schedules)
        {
            schedules.Add(new ParkingSchedule(
                request.SubmissionId,
                item.DayOfWeek,
                item.StartTime,
                item.EndTime
            ));
        }

        foreach (var schedule in schedules)
        {
            await _parkingScheduleRepository.AddAsync(schedule);
        }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SchedulePrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.UserPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, cancellationToken);
        }

        return Result<Guid>.Success(Guid.NewGuid(), "Parking schedules created.");
    }
}
