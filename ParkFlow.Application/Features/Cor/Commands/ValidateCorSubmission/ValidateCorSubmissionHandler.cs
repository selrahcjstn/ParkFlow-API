using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;

public class ValidateCorSubmissionHandler : IRequestHandler<ValidateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IValidator<ValidateCorSubmissionCommand> _validator;
    private readonly INotificationService? _notificationService;

    public ValidateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IUserAccountRepository userAccountRepository,
        IVehicleRepository vehicleRepository,
        IValidator<ValidateCorSubmissionCommand> validator,
        INotificationService? notificationService = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _userAccountRepository = userAccountRepository;
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _notificationService = notificationService;
    }

    public async Task<Result<Guid>> Handle(ValidateCorSubmissionCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        // 1. Try to find submission by submission ID
        var submission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId);

        // 2. If not found, try finding latest submission by user ID
        if (submission == null)
        {
            submission = await _corSubmissionRepository.GetLatestByUserIdAsync(request.CorSubmissionId);
        }

        UserAccount? user = null;
        if (submission != null)
        {
            submission.UpdateSubmission(null, null, request.VerificationStatus);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(submission);
            user = await _userAccountRepository.GetByIdAsync(submission.UserAccountId);
        }
        else
        {
            // If no submission exists, check if request ID is a direct UserAccountId
            user = await _userAccountRepository.GetByIdAsync(request.CorSubmissionId);
        }

        if (user != null)
        {
            if (request.VerificationStatus == CorVerificationStatus.Verified)
            {
                user.Verify();
            }
            else if (request.VerificationStatus == CorVerificationStatus.Rejected)
            {
                user.UpdateStatus(AccountStatus.PendingVerification);
            }
            await _userAccountRepository.UpdateAsync(user);

            var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(user.Id);
            foreach (var vehicle in userVehicles)
            {
                vehicle.UpdateVerificationStatus(request.VerificationStatus);
                await _vehicleRepository.UpdateAsync(vehicle);
            }

            if (_notificationService != null)
            {
                var isApproved = request.VerificationStatus == CorVerificationStatus.Verified;
                var title = isApproved ? "Schedule & COR Verified" : "Schedule & COR Verification Declined";
                var subtitle = isApproved ? "Pass Activated" : "Resubmission Required";
                var body = isApproved
                    ? "Your submitted Certificate of Registration (COR) and duty schedule have been verified by security administration."
                    : "Your COR document was declined by security administration. Please re-upload clear documentation.";
                var actionRoute = isApproved ? "/(settings)/schedule" : "/(settings)/setup-schedule";
                var actionText = isApproved ? "View Pass Details" : "Resubmit COR Document";
                var type = isApproved ? "approved" : "rejected";

                await _notificationService.CreateAndSendNotificationAsync(
                    user.Id,
                    title,
                    body,
                    type: type,
                    subtitle: subtitle,
                    actionRoute: actionRoute,
                    actionText: actionText,
                    priority: "high",
                    issuer: "ParkFlow Document Desk"
                );
            }
        }

        if (submission == null && user == null)
        {
            return Result<Guid>.Failure("COR submission or user account not found.", ErrorCode.NotFound);
        }

        return Result<Guid>.Success(submission?.Id ?? user!.Id, $"COR submission validation updated to {request.VerificationStatus}.");
    }
}
