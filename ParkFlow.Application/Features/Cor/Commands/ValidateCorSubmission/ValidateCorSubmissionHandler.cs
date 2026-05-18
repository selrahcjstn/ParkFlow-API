using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;

public class ValidateCorSubmissionHandler : IRequestHandler<ValidateCorSubmissionCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IValidator<ValidateCorSubmissionCommand> _validator;

    public ValidateCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IValidator<ValidateCorSubmissionCommand> validator)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(ValidateCorSubmissionCommand request, CancellationToken cancellationToken)
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

        submission.UpdateSubmission(null, null, request.VerificationStatus);

        await _corSubmissionRepository.UpdateCorSubmissionAsync(submission);

        return Result<Guid>.Success(submission.Id, $"COR submission validation updated to {request.VerificationStatus}.");
    }
}
