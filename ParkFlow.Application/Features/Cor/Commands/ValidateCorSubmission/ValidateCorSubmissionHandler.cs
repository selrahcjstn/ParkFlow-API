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
    private readonly IEmailService? _emailService;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    private readonly ICacheService? _cacheService;

    public ValidateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IUserAccountRepository userAccountRepository,
        IVehicleRepository vehicleRepository,
        IValidator<ValidateCorSubmissionCommand> validator,
        INotificationService? notificationService = null,
        IEmailService? emailService = null,
        ISignalRNotificationSender? signalRNotificationSender = null,
        ICacheService? cacheService = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _userAccountRepository = userAccountRepository;
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _notificationService = notificationService;
        _emailService = emailService;
        _signalRNotificationSender = signalRNotificationSender;
        _cacheService = cacheService;
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
            submission.UpdateSubmission(null, null, request.VerificationStatus, rejectionReason: request.RejectionReason);
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
                var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(user.Id);
                var hasVerifiedVehicle = userVehicles.Any(v => v.VerificationStatus == CorVerificationStatus.Verified);
                if (hasVerifiedVehicle)
                {
                    user.Verify();
                }

                if (submission == null)
                {
                    submission = new CorSubmission(user.Id, "Academic Term", string.Empty, verificationStatus: CorVerificationStatus.Verified);
                    await _corSubmissionRepository.AddCorSubmissionAsync(submission);
                }
            }
            else if (request.VerificationStatus == CorVerificationStatus.Rejected)
            {
                user.UpdateStatus(AccountStatus.PendingVerification);
            }
            await _userAccountRepository.UpdateAsync(user);

            var isApproved = request.VerificationStatus == CorVerificationStatus.Verified;

            if (_signalRNotificationSender != null)
            {
                var eventData = new
                {
                    userId = user.Id,
                    type = isApproved ? "approved" : "registration_rejected",
                    title = isApproved ? "Registration & COR Verified" : "Registration Verification Rejected",
                    body = isApproved ? "Your account and parking pass have been verified." : (request.RejectionReason ?? "Your registration was rejected.")
                };
                try
                {
                    await _signalRNotificationSender.SendToUserAsync(user.Id.ToString(), "VerificationStatusChanged", eventData);
                    await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", eventData);
                }
                catch
                {
                    // Ignore transient SignalR send failures
                }
            }

            if (_notificationService != null)
            {
                var title = isApproved ? "Schedule & COR Verified" : "Registration Verification Rejected";
                var subtitle = isApproved ? "Schedule Verified" : "Action Required";
                var body = isApproved
                    ? "Your submitted Certificate of Registration (COR) and duty schedule have been verified by security administration."
                    : string.IsNullOrWhiteSpace(request.RejectionReason)
                        ? "Your registration was rejected by the admin. Please review the rejection reason and re-upload the required documents to continue the verification process."
                        : $"Your registration was rejected by the admin. Reason: {request.RejectionReason}. Please re-upload the required documents to continue the verification process.";
                var actionRoute = isApproved ? "/(settings)/schedule" : "/(settings)/setup-schedule";
                var actionText = isApproved ? "View Pass Details" : "Re-upload Documents";
                var type = isApproved ? "approved" : "registration_rejected";

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

            if (_emailService != null && !string.IsNullOrWhiteSpace(user.PrimaryEmail))
            {
                try
                {
                    var emailSubject = isApproved
                        ? "ParkFlow - Registration & COR Verified"
                        : "ParkFlow - Registration Verification Rejected";

                    var reasonBlock = !isApproved && !string.IsNullOrWhiteSpace(request.RejectionReason)
                        ? $"<div style='background-color:#FEF2F2; border-left:4px solid #EF4444; padding:12px; margin:16px 0; font-family:sans-serif;'><strong>Rejection Reason:</strong> {request.RejectionReason}</div>"
                        : "";

                    var emailBody = isApproved
                        ? $@"
                            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #E2E8F0; border-radius:12px;'>
                              <h2 style='color:#10B981; margin-top:0;'>Registration Verified</h2>
                              <p>Hello,</p>
                              <p>Your submitted Certificate of Registration (COR) and duty schedule have been successfully verified by ParkFlow Security Administration.</p>
                              <p>Your digital parking pass is now activated.</p>
                              <p style='color:#64748B; font-size:12px; margin-top:24px;'>ParkFlow Security Administration</p>
                            </div>"
                        : $@"
                            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #E2E8F0; border-radius:12px;'>
                              <h2 style='color:#EF4444; margin-top:0;'>Registration Rejected</h2>
                              <p>Hello,</p>
                              <p>Your registration was rejected by the admin. Please review the rejection reason and re-upload the required documents to continue the verification process.</p>
                              {reasonBlock}
                              <p>Please log in to the ParkFlow mobile app to update and re-upload your verification documents.</p>
                              <p style='color:#64748B; font-size:12px; margin-top:24px;'>ParkFlow Security Administration</p>
                            </div>";

                    await _emailService.SendEmailAsync(user.PrimaryEmail, emailSubject, emailBody);
                }
                catch
                {
                    // Ignore email dispatch failure
                }
            }
        }

        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "schedule",
                    submissionId = submission?.Id ?? user?.Id,
                    userId = user?.Id,
                    status = request.VerificationStatus.ToString()
                });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.UserPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SchedulePrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.VehiclePrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix, cancellationToken);
        }

        return Result<Guid>.Success(submission?.Id ?? user!.Id, $"COR submission validation updated to {request.VerificationStatus}.");
    }
}
