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

        var existing = await _corSubmissionRepository.GetByUserIdAndTermAsync(request.UserId, request.AcademicTerm);
        if (existing == null)
        {
            var submission = new CorSubmission(request.UserId, request.AcademicTerm, request.CorDocumentUrl, request.OrcrDocumentUrl, request.MotorPictureUrl);
            await _corSubmissionRepository.AddCorSubmissionAsync(submission);
            existing = submission;
        }
        else
        {
            existing.UpdateSubmission(request.AcademicTerm, request.CorDocumentUrl, existing.VerificationStatus, request.OrcrDocumentUrl, request.MotorPictureUrl);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(existing);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Schedule);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<Guid>.Success(existing.Id, "COR onboarding completed.");
    }
}
