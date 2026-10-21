using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Application.Features.ParkingLogs.Services;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;

public class UpdateOnboardingScheduleHandler : IRequestHandler<UpdateOnboardingScheduleCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<UpdateOnboardingScheduleCommand> _validator;

    private readonly ICacheService? _cacheService;

    public UpdateOnboardingScheduleHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingScheduleCommand> validator,
        ICacheService? cacheService = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
        _cacheService = cacheService;
    }

    public Task<Result<Guid>> Handle(UpdateOnboardingScheduleCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result<Guid>.Failure("Schedules are read-only. Students must confirm the extracted COR schedule or upload a clearer COR.", ErrorCode.Forbidden));
}
