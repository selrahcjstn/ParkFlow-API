using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Features.Files.Commands.UploadCorDocument;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Application.Features.Schedules.Command;
using ParkFlow.Application.Features.Auth.Commands.VerifyEmailOtp;
using ParkFlow.Application.Features.Profiles.Commands.UpdateUserProfile;
using ParkFlow.Application.Features.Users.Commands.UpdateUserAccountAdmin;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Verification;

// A single MediatR hook covers existing submission routes without duplicating OCR work
// in their handlers. Database enqueue is idempotent for the same source fingerprint.
public class VerificationSubmissionBehavior<TRequest, TResponse>(VerificationService service,
    ICorSubmissionRepository cors, IUserContext userContext, IAuthIdentityRepository identities,
    IUserAccountRepository users, IVerificationStore verification)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next(ct);
        var success = response switch { Result<Guid> r => r.IsSuccess, Result<bool> r => r.IsSuccess,
            Result<UploadFileResponse> r => r.IsSuccess, _ => false };
        if (!success) return response;
        var id = response is Result<Guid> guid ? guid.Data : Guid.Empty;
        switch (request)
        {
            case UploadCorDocumentCommand:
                var latest = await cors.GetLatestByUserIdAsync(userContext.GetUserId());
                if (latest != null) await service.EnqueueAsync(VerificationType.Cor, latest.Id, ct);
                break;
            case UpdateOnboardingCorCommand r:
                await service.EnqueueAsync(VerificationType.Cor, id, ct);
                if ((await users.GetByIdAsync(r.UserId))?.Status == ParkFlow.Domain.Enums.AccountStatus.PendingVerification)
                    await service.EnqueueAsync(VerificationType.Account, r.UserId, ct);
                break;
            case CreateCorSubmissionCommand or UpdateCorSubmissionCommand:
                await service.EnqueueAsync(VerificationType.Cor, id, ct); break;
            case UpdateOnboardingScheduleCommand:
                await service.EnqueueAsync(VerificationType.Schedule, id, ct); break;
            case UpdateCorSchedulesCommand r:
                var cor = await cors.GetCorSubmissionAsync(r.CorSubmissionId) ?? await cors.GetLatestByUserIdAsync(r.CorSubmissionId);
                if (cor != null) await service.EnqueueAsync(VerificationType.Schedule, cor.Id, ct);
                break;
            case CreateParkingScheduleCommand r:
                await service.EnqueueAsync(VerificationType.Schedule, r.SubmissionId, ct); break;
            case UpdateOnboardingProfileCommand r: await RefreshIdentity(r.UserId); break;
            case UpdateOnboardingStudentCommand r: await RefreshIdentity(r.UserId); break;
            case UpdateOnboardingPersonnelCommand r: await RefreshIdentity(r.UserId); break;
            case UpdateUserProfileCommand r: await RefreshIdentity(r.UserId); break;
            case UpdateUserAccountAdminCommand r: await RefreshIdentity(r.UserId); break;
            case VerifyEmailOtpCommand r:
                if (string.IsNullOrEmpty(r.Purpose) || r.Purpose.Equals("Verification", StringComparison.OrdinalIgnoreCase))
                {
                    var identity = await identities.GetByEmailAsync(r.Email);
                    if (identity != null) await RefreshIdentity(identity.UserAccountId);
                }
                break;
        }
        return response;

        async Task RefreshIdentity(Guid owner)
        {
            var active = (await users.GetByIdAsync(owner))?.Status == ParkFlow.Domain.Enums.AccountStatus.Active;
            var current = await verification.ListAsync(owner, false, ct);
            if (!active || current.Any(r => r.Type == VerificationType.Account))
                await service.EnqueueAsync(VerificationType.Account, owner, ct);
            var latest = await cors.GetLatestByUserIdAsync(owner);
            if (latest != null && latest.CorDocumentUrl != "pending" && (!active || current.Any(r => r.Type == VerificationType.Cor)))
                await service.EnqueueAsync(VerificationType.Cor, latest.Id, ct);
        }
    }
}
