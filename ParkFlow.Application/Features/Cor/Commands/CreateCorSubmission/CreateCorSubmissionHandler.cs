using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;

public class CreateCorSubmissionHandler : IRequestHandler<CreateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IValidator<CreateCorSubmissionCommand> _validator;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public CreateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<CreateCorSubmissionCommand> validator,
        IVehicleRepository? vehicleRepository = null,
        IUserAccountRepository? userAccountRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
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

        // Reset all vehicles for this user to Pending (Unverified)
        if (_vehicleRepository != null)
        {
            var vehicles = await _vehicleRepository.GetByOwnerIdAsync(request.UserId);
            foreach (var vehicle in vehicles)
            {
                vehicle.UpdateVerificationStatus(ParkFlow.Domain.Enums.CorVerificationStatus.Pending);
                await _vehicleRepository.UpdateAsync(vehicle);
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

                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "vehicle",
                    ownerId = request.UserId,
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
