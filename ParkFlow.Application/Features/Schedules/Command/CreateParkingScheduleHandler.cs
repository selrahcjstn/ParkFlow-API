using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.ParkingLogs.Services;

namespace ParkFlow.Application.Features.Schedules.Command;

public class CreateParkingScheduleHandler : IRequestHandler<CreateParkingScheduleCommand, Result<Guid>>
{
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IValidator<CreateParkingScheduleCommand> _validator;

    private readonly ICacheService? _cacheService;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IUserAccountRepository _userAccountRepository;

    public CreateParkingScheduleHandler(
        IParkingScheduleRepository parkingScheduleRepository,
        IValidator<CreateParkingScheduleCommand> validator,
        ICorSubmissionRepository corSubmissionRepository,
        IUserAccountRepository userAccountRepository,
        ICacheService? cacheService = null)
    {
        _parkingScheduleRepository = parkingScheduleRepository;
        _validator = validator;
        _cacheService = cacheService;
        _corSubmissionRepository = corSubmissionRepository;
        _userAccountRepository = userAccountRepository;
    }

    public Task<Result<Guid>> Handle(CreateParkingScheduleCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result<Guid>.Failure("Schedules are read-only. Students must confirm the extracted COR schedule or upload a clearer COR.", ErrorCode.Forbidden));
}
