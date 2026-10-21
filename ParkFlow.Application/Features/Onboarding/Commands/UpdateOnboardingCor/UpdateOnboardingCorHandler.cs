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
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;
    private readonly ICacheService? _cacheService;

    public UpdateOnboardingCorHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingCorCommand> validator,
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IVehicleRepository? vehicleRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null,
        ICacheService? cacheService = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _vehicleRepository = vehicleRepository;
        _signalRNotificationSender = signalRNotificationSender;
        _cacheService = cacheService;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingCorCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        // Do not let older clients or a skipped role step complete an invalid registration.
        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (profile == null)
            return Result<Guid>.Failure("Please complete your profile and ID information before submitting documents.", ErrorCode.BadRequest);

        var student = await _studentRepository.GetByUserProfileIdAsync(profile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(profile.Id);
        if (student == null && personnel == null)
            return Result<Guid>.Failure("Please save your student or employee ID number before submitting documents.", ErrorCode.BadRequest);

        if (student != null && string.IsNullOrWhiteSpace(student.StudentNumber))
            return Result<Guid>.Failure("Please save your student ID number before submitting documents.", ErrorCode.BadRequest);
        if (student != null && await _studentRepository.StudentNumberExistsAsync(student.StudentNumber, profile.Id))
            return Result<Guid>.Failure("This student ID number is already registered to another account. Please check your ID information or contact campus administration.", ErrorCode.Conflict);

        if (personnel != null && string.IsNullOrWhiteSpace(personnel.IdCardNumber))
            return Result<Guid>.Failure("Please save your employee ID number before submitting documents.", ErrorCode.BadRequest);
        if (personnel != null && await _personnelRepository.IdCardNumberExistsAsync(personnel.IdCardNumber, profile.Id))
            return Result<Guid>.Failure("This employee ID number is already registered to another account. Please check your ID information or contact campus administration.", ErrorCode.Conflict);

        var existing = await _corSubmissionRepository.GetLatestByUserIdAsync(request.UserId);

        if (existing == null || !string.Equals(existing.CorDocumentUrl, request.CorDocumentUrl, StringComparison.Ordinal))
            return Result<Guid>.Failure("Upload your document using ParkFlow before submitting it.", ErrorCode.BadRequest);

        var corUrl = !string.IsNullOrWhiteSpace(request.CorDocumentUrl) && !string.Equals(request.CorDocumentUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase)
            ? request.CorDocumentUrl
            : existing?.CorDocumentUrl;

        var orcrUrl = !string.IsNullOrWhiteSpace(request.OrcrDocumentUrl) && !string.Equals(request.OrcrDocumentUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase)
            ? request.OrcrDocumentUrl
            : existing?.OrcrDocumentUrl;

        var motorUrl = !string.IsNullOrWhiteSpace(request.MotorPictureUrl) && !string.Equals(request.MotorPictureUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase)
            ? request.MotorPictureUrl
            : existing?.MotorPictureUrl;

        if (string.IsNullOrWhiteSpace(corUrl) || string.Equals(corUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Result<Guid>.Failure("COR document is required.", ErrorCode.BadRequest);
        }

        if (existing == null)
        {
            var submission = new CorSubmission(request.UserId, request.AcademicTerm, corUrl, orcrUrl, motorUrl, CorVerificationStatus.Pending);
            await _corSubmissionRepository.AddCorSubmissionAsync(submission);
            existing = submission;
        }
        else
        {
            var documentStatus = existing.VerificationStatus == CorVerificationStatus.NotSubmitted ? CorVerificationStatus.Pending : existing.VerificationStatus;
            existing.UpdateSubmission(request.AcademicTerm, corUrl, documentStatus, orcrUrl, motorUrl);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(existing);
        }

        if (_vehicleRepository != null)
        {
            try
            {
                var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(request.UserId);
                var primaryVehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();
                if (primaryVehicle != null &&
                    ((!string.IsNullOrWhiteSpace(orcrUrl) && orcrUrl != "pending" && orcrUrl != primaryVehicle.OrcrDocumentUrl) ||
                     (!string.IsNullOrWhiteSpace(motorUrl) && motorUrl != "pending" && motorUrl != primaryVehicle.VehiclePictureUrl)))
                {
                    primaryVehicle.UpdateDocuments(orcrUrl, motorUrl);
                    await _vehicleRepository.UpdateAsync(primaryVehicle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateOnboardingCor] Error updating vehicle documents: {ex.Message}");
            }
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Done);
            await _userAccountRepository.UpdateAsync(user);
        }

        // Clear stale onboarding details before clients refresh in response to realtime events.
        if (_cacheService != null)
        {
            await _cacheService.RemoveAsync(CacheKeys.UserProfile(request.UserId), cancellationToken);
            await _cacheService.RemoveAsync(CacheKeys.UserVehicles(request.UserId), cancellationToken);
        }

        if (_signalRNotificationSender != null)
        {
            try
            {
                if (student == null) await _signalRNotificationSender.SendToAllAsync("RegistrationSubmitted", new
                {
                    userId = request.UserId,
                    academicTerm = request.AcademicTerm
                });

                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = student != null ? "Vehicle" : "registration",
                    userId = request.UserId
                });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        return Result<Guid>.Success(existing.Id, "COR onboarding completed.");
    }
}
