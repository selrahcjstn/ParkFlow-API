using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ParkFlow.Application.Interfaces;

public interface ICloudinaryService
{
    Task<(string SecureUrl, string PublicId)> UploadImageAsync(IFormFile file, string folder);
    Task<(string SecureUrl, string PublicId)> UploadPdfAsync(IFormFile file, string folder);
    Task DeleteFileAsync(string publicId, bool isImage = true);
}
