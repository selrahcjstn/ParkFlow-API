using MediatR;
using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
namespace ParkFlow.Application.Features.Profiles.Commands;

public record SubmitDriverDocuments(Guid UserId, IFormFile? License, IFormFile? Photo) : IRequest<Result<bool>>;
public class SubmitDriverDocumentsHandler(IDriverDocumentRepository repository, IUserProfileRepository profiles,
    ICloudinaryService cloud, ICacheService cache, ISignalRNotificationSender signalR) : IRequestHandler<SubmitDriverDocuments, Result<bool>>
{
    public async Task<Result<bool>> Handle(SubmitDriverDocuments r, CancellationToken ct)
    {
        if (r.License == null && r.Photo == null) return Result<bool>.Failure("Choose a driver's license or profile photo.", ErrorCode.BadRequest);
        if (await profiles.GetByUserIdAsync(r.UserId) == null) return Result<bool>.Failure("Profile not found.", ErrorCode.NotFound);
        try
        {
            var licenseUrl = r.License == null ? null : await DriverDocumentUpload.UploadAsync(r.License, true, cloud, ct);
            var photoUrl = r.Photo == null ? null : await DriverDocumentUpload.UploadAsync(r.Photo, false, cloud, ct);
            if (!await repository.SubmitAsync(r.UserId, licenseUrl, photoUrl, ct)) return Result<bool>.Failure("Profile changed. Refresh and retry.", ErrorCode.Conflict);
            await cache.RemoveByPrefixAsync(CacheKeys.UserPrefix, ct);
            await cache.RemoveByPrefixAsync(CacheKeys.VehiclePrefix, ct);
            try { await signalR.SendToAllAsync("ApprovalListUpdated", new { type = "Vehicle", userId = r.UserId }); } catch { }
            return Result<bool>.Success(true, "Driver documents saved. New licenses require admin review in Vehicles.");
        }
        catch (DocumentValidationException e) { return Result<bool>.Failure(e.Message, ErrorCode.BadRequest); }
    }
}
