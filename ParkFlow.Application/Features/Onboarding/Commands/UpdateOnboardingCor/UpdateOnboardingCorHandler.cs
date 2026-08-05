using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;

public class UpdateOnboardingCorHandler : IRequestHandler<UpdateOnboardingCorCommand, Result<Guid>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<UpdateOnboardingCorCommand> _validator;

    public UpdateOnboardingCorHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingCorCommand> validator)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingCorCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var existing = await _corSubmissionRepository.GetLatestByUserIdAsync(request.UserId);

        var corUrl = !string.IsNullOrWhiteSpace(request.CorDocumentUrl) && !string.Equals(request.CorDocumentUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase)
            ? request.CorDocumentUrl
            : existing?.CorDocumentUrl;

        var orcrUrl = !string.IsNullOrWhiteSpace(request.OrcrDocumentUrl) && !string.Equals(request.OrcrDocumentUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase)
            ? request.OrcrDocumentUrl
            : existing?.OrcrDocumentUrl;

        var motorUrl = !string.IsNullOrWhiteSpace(request.MotorPictureUrl) && !string.Equals(request.MotorPictureUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase)
            ? request.MotorPictureUrl
            : existing?.MotorPictureUrl;

        if (string.IsNullOrWhiteSpace(corUrl) || string.Equals(corUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(orcrUrl) || string.Equals(orcrUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(motorUrl) || string.Equals(motorUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Result<Guid>.Failure("Registration cannot be submitted without uploading all required documents (COR/ID, OR/CR, and Vehicle Picture).", ErrorCode.BadRequest);
        }

        if (existing == null)
        {
            var submission = new CorSubmission(request.UserId, request.AcademicTerm, corUrl, orcrUrl, motorUrl, CorVerificationStatus.Pending);
            await _corSubmissionRepository.AddCorSubmissionAsync(submission);
            existing = submission;
        }
        else
        {
            existing.UpdateSubmission(request.AcademicTerm, corUrl, CorVerificationStatus.Pending, orcrUrl, motorUrl);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(existing);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Done);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<Guid>.Success(existing.Id, "COR onboarding completed.");
    }
}
