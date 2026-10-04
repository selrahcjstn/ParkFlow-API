using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;

public class CreateCorSubmissionHandler : IRequestHandler<CreateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingLogRepository? _parkingLogRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IValidator<CreateCorSubmissionCommand> _validator;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public CreateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<CreateCorSubmissionCommand> validator,
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

    public async Task<Result<Guid>> Handle(CreateCorSubmissionCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        if (_parkingLogRepository != null && await _parkingLogRepository.HasActiveParkingLogByUserIdAsync(request.UserId))
        {
            return Result<Guid>.Failure("Cannot submit or update Certificate of Registration (COR) while you have an ongoing parking session.", ErrorCode.BadRequest);
        }

        var existingSubmission = await _corSubmissionRepository
            .GetByUserIdAndTermAsync(request.UserId, request.AcademicTerm);

        if (existingSubmission != null)
        {
            return Result<Guid>.Failure(
                "A COR submission for this user and term already exists.",
                ErrorCode.Conflict);
        }

        var corSubmission = new CorSubmission
        (
            request.UserId,
            request.AcademicTerm,
            request.CorDocumentUrl
        );

        await _corSubmissionRepository.AddCorSubmissionAsync(corSubmission);

        // Reset user account status to PendingVerification
        if (_userAccountRepository != null)
        {
            var user = await _userAccountRepository.GetByIdAsync(request.UserId);
            if (user != null)
            {
                user.UpdateStatus(ParkFlow.Domain.Enums.AccountStatus.PendingVerification);
                await _userAccountRepository.UpdateAsync(user);
            }
        }

        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("ScheduleSubmitted", new
                {
                    submissionId = corSubmission.Id,
                    userId = request.UserId,
                    academicTerm = request.AcademicTerm
                });

                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "schedule",
                    submissionId = corSubmission.Id,
                    status = "Pending"
                });

                await _signalRNotificationSender.SendToUserAsync(request.UserId.ToString(), "VerificationStatusChanged", new
                {
                    userId = request.UserId,
                    type = "cor_updated",
                    status = "Pending"
                });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        return Result<Guid>.Success(corSubmission.Id, "COR submission created.");
    }
}
