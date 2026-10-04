using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.DeleteCorSubmission;

public class DeleteCorSubmissionHandler : IRequestHandler<DeleteCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingLogRepository? _parkingLogRepository;
    private readonly IValidator<DeleteCorSubmissionCommand> _validator;

    public DeleteCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<DeleteCorSubmissionCommand> validator,
        IParkingLogRepository? parkingLogRepository = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<Guid>> Handle(DeleteCorSubmissionCommand request, CancellationToken cancellationToken)
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
            return Result<Guid>.Failure("Cannot delete Certificate of Registration (COR) while you have an ongoing parking session.", ErrorCode.BadRequest);
        }

        await _corSubmissionRepository.DeleteCorSubmissionAsync(submission);

        return Result<Guid>.Success(submission.Id, "COR submission deleted.");
    }
}
