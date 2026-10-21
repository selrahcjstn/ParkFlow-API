using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;

public class ValidateCorSubmissionHandler : IRequestHandler<ValidateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository cors;
    private readonly IValidator<ValidateCorSubmissionCommand> validator;
    private readonly VerificationService? verification;
    private readonly IUserContext? reviewer;
    public ValidateCorSubmissionHandler(ICorSubmissionRepository corSubmissionRepository, IUserAccountRepository userAccountRepository,
        IVehicleRepository vehicleRepository, IValidator<ValidateCorSubmissionCommand> validator,
        INotificationService? notificationService = null, IEmailService? emailService = null,
        ISignalRNotificationSender? signalRNotificationSender = null, ICacheService? cacheService = null,
        VerificationService? verificationService = null, IUserContext? userContext = null)
    { cors = corSubmissionRepository; this.validator = validator; verification = verificationService; reviewer = userContext; }

    public async Task<Result<Guid>> Handle(ValidateCorSubmissionCommand request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<Guid>.Failure(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)), ErrorCode.BadRequest);
        var submission = await cors.GetCorSubmissionAsync(request.CorSubmissionId) ?? await cors.GetLatestByUserIdAsync(request.CorSubmissionId);
        if (submission == null) return Result<Guid>.Failure("COR submission not found.", ErrorCode.NotFound);
        if (verification != null)
        {
            try
            {
                var status = request.VerificationStatus == CorVerificationStatus.Verified ? VerificationState.Approved : VerificationState.Rejected;
                if (!await verification.ReviewSubjectAsync(VerificationType.Cor, submission.Id, status,
                    request.RejectionReason ?? (status == VerificationState.Approved ? "Document reviewed and approved by an administrator." : "Document rejected by an administrator."), reviewer!.GetUserId(), ct))
                    return Result<Guid>.Failure("Submission changed. Reload before reviewing.", ErrorCode.Conflict);
            }
            catch (DocumentValidationException ex) { return Result<Guid>.Failure(ex.Message, ErrorCode.BadRequest); }
        }
        else
        {
            submission.UpdateSubmission(null, null, request.VerificationStatus, rejectionReason: request.RejectionReason);
            await cors.UpdateCorSubmissionAsync(submission);
        }
        return Result<Guid>.Success(submission.Id, "COR decision saved. Account, vehicle and schedule decisions are independent.");
    }
}
