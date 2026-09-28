using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;

public class CreateCorSubmissionHandler : IRequestHandler<CreateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IValidator<CreateCorSubmissionCommand> _validator;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public CreateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<CreateCorSubmissionCommand> validator,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
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
                    submissionId = corSubmission.Id
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
