using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;
using ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;
namespace ParkFlow.Application.Features.Profiles;

public static class DriverDocumentUpload
{
    public static async Task<string> UploadAsync(IFormFile file, bool license, ICloudinaryService cloud, CancellationToken ct)
    {
        var validation = license
            ? await new UploadOrcrDocumentCommandValidator().ValidateAsync(new UploadOrcrDocumentCommand(file), ct)
            : await new UploadMotorPictureCommandValidator().ValidateAsync(new UploadMotorPictureCommand(file), ct);
        if (!validation.IsValid) throw new DocumentValidationException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        var folder = license ? "parkflow/licenses" : "parkflow/profile-pictures";
        var uploaded = Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? await cloud.UploadPdfAsync(file, folder) : await cloud.UploadImageAsync(file, folder);
        return uploaded.SecureUrl;
    }
}
