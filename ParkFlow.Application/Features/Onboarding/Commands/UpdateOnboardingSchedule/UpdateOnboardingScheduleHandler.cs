using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;

public class UpdateOnboardingScheduleHandler : IRequestHandler<UpdateOnboardingScheduleCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<UpdateOnboardingScheduleCommand> _validator;

    public UpdateOnboardingScheduleHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingScheduleCommand> validator)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingScheduleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var submission = await _corSubmissionRepository.GetByUserIdAndTermAsync(request.UserId, string.Empty);
        if (submission == null)
            return Result<Guid>.Failure("COR submission not found.", ErrorCode.NotFound);

        var existingSchedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(submission.Id);
        foreach (var existing in existingSchedules)
        {
            await _parkingScheduleRepository.DeleteAsync(existing);
        }

        foreach (var item in request.Items)
        {
            var schedule = new ParkingSchedule(submission.Id, item.DayOfWeek, item.StartTime, item.EndTime);
            await _parkingScheduleRepository.AddAsync(schedule);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Done);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<Guid>.Success(submission.Id, "Schedule onboarding completed.");
    }
}
