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

    public async Task<Result<Guid>> Handle(UpdateOnboardingScheduleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<Guid>.Failure("Account not found.", ErrorCode.NotFound);
        if (PersonnelParkingPolicy.AppliesTo(user.UserProfile?.Personnel))
            return Result<Guid>.Failure(
                "Faculty and university staff parking hours are managed by the Super Admin. No personal schedule is required.",
                ErrorCode.Forbidden);

        var submission = await _corSubmissionRepository.GetLatestByUserIdAsync(request.UserId);
        if (submission == null)
        {
            submission = new CorSubmission(request.UserId, "2024-2025", "pending");
            await _corSubmissionRepository.AddCorSubmissionAsync(submission);
        }

        var newSchedules = request.Items.Select(item =>
            new ParkingSchedule(submission.Id, item.DayOfWeek, item.StartTime, item.EndTime)
        ).ToList();

        await _parkingScheduleRepository.ReplaceSchedulesAsync(submission.Id, newSchedules);

        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Done);
            await _userAccountRepository.UpdateAsync(user);
        }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SchedulePrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.UserPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, cancellationToken);
        }

        return Result<Guid>.Success(submission.Id, "Schedule onboarding completed.");
    }
}
