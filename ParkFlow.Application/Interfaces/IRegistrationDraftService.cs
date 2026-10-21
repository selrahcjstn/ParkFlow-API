using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;
using ParkFlow.Application.Features.Verification;

namespace ParkFlow.Application.Interfaces;

public record RegistrationSession(string Token, DateTime ExpiresAt);
public interface IRegistrationDraftService
{
    Task<RegistrationSession> StartAsync(string email, string emailVerificationToken, CancellationToken ct);
    Task<CorSchedulePreview> UploadCorAsync(string token, IFormFile file, CancellationToken ct);
    Task<CorSchedulePreview?> PreviewAsync(string token, CancellationToken ct);
    Task<CorSchedulePreview> FetchAsync(string token, Guid recordId, int version, CancellationToken ct);
    Task<Result<string>> CompleteAsync(string token, RegistrationDetails details, IFormFile? cor, IFormFile orcr, IFormFile motor, IFormFile license, IFormFile profilePhoto, CancellationToken ct);
    Task CancelAsync(string token, CancellationToken ct);
}
