using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;

public class UpdateCorSubmissionHandler : IRequestHandler<UpdateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IValidator<UpdateCorSubmissionCommand> _validator;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public UpdateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<UpdateCorSubmissionCommand> validator,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
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

        submission.UpdateSubmission(
            request.AcademicTerm,
            request.CorDocumentUrl,
            request.VerificationStatus);

        await _corSubmissionRepository.UpdateCorSubmissionAsync(submission);

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
                    submissionId = submission.Id
                });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        return Result<Guid>.Success(submission.Id, "COR submission updated.");
    }
}
