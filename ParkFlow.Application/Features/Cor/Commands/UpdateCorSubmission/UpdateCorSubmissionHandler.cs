using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;

public class UpdateCorSubmissionHandler : IRequestHandler<UpdateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingLogRepository? _parkingLogRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IValidator<UpdateCorSubmissionCommand> _validator;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public UpdateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<UpdateCorSubmissionCommand> validator,
        IParkingLogRepository? parkingLogRepository = null,
        IVehicleRepository? vehicleRepository = null,
        IUserAccountRepository? userAccountRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userAccountRepository = userAccountRepository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<Guid>> Handle(UpdateCorSubmissionCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var submission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId);

        if (submission == null)
            return Result<Guid>.Failure("COR submission not found.", ErrorCode.NotFound);

        if (_parkingLogRepository != null && await _parkingLogRepository.HasActiveParkingLogByUserIdAsync(submission.UserAccountId))
        {
            return Result<Guid>.Failure("Cannot update Certificate of Registration (COR) while you have an ongoing parking session.", ErrorCode.BadRequest);
        }

        var statusToSet = request.VerificationStatus ?? ParkFlow.Domain.Enums.CorVerificationStatus.Pending;

        submission.UpdateSubmission(
            request.AcademicTerm,
            request.CorDocumentUrl,
            statusToSet);

        await _corSubmissionRepository.UpdateCorSubmissionAsync(submission);

        if (statusToSet == ParkFlow.Domain.Enums.CorVerificationStatus.Pending)
        {
            // Reset user account status to PendingVerification
            if (_userAccountRepository != null)
            {
                var user = await _userAccountRepository.GetByIdAsync(submission.UserAccountId);
                if (user != null)
                {
                    user.UpdateStatus(ParkFlow.Domain.Enums.AccountStatus.PendingVerification);
                    await _userAccountRepository.UpdateAsync(user);
                }
            }
        }

        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("ScheduleSubmitted", new
                {
                    submissionId = submission.Id,
                    userId = submission.UserAccountId,
                    academicTerm = request.AcademicTerm
                });

                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "schedule",
                    submissionId = submission.Id,
                    status = request.VerificationStatus.ToString()
                });

                if (request.VerificationStatus == ParkFlow.Domain.Enums.CorVerificationStatus.Pending)
                {
                    await _signalRNotificationSender.SendToUserAsync(submission.UserAccountId.ToString(), "VerificationStatusChanged", new
                    {
                        userId = submission.UserAccountId,
                        type = "cor_updated",
                        status = "Pending"
                    });
                }
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        return Result<Guid>.Success(submission.Id, "COR submission updated.");
    }
}
