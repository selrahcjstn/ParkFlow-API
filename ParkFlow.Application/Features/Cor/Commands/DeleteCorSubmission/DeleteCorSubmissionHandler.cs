using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.DeleteCorSubmission;

public class DeleteCorSubmissionHandler : IRequestHandler<DeleteCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IValidator<DeleteCorSubmissionCommand> _validator;

    public DeleteCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<DeleteCorSubmissionCommand> validator)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
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

        await _corSubmissionRepository.DeleteCorSubmissionAsync(submission);

        return Result<Guid>.Success(submission.Id, "COR submission deleted.");
    }
}
