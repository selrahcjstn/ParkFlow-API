using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ParkFlow.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace ParkFlow.Infrastructure.Cloudinary;

public class CloudinaryService : ICloudinaryService
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;

    public CloudinaryService(IOptions<CloudinarySettings> config)
    {
        var account = new Account(
            config.Value.CloudName,
            config.Value.ApiKey,
            config.Value.ApiSecret
        );

        _cloudinary = new CloudinaryDotNet.Cloudinary(account);
    }

    public async Task<(string SecureUrl, string PublicId)> UploadImageAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.", nameof(file));

        using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null)
        {
            throw new Exception($"Cloudinary image upload failed: {result.Error.Message}");
        }

        return (result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task<(string SecureUrl, string PublicId)> UploadPdfAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.", nameof(file));

        using var stream = file.OpenReadStream();

        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null)
        {
            throw new Exception($"Cloudinary PDF upload failed: {result.Error.Message}");
        }

        return (result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteFileAsync(string publicId, bool isImage = true)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            throw new ArgumentException("Public ID cannot be null or empty.", nameof(publicId));

        var deletionParams = new DeletionParams(publicId)
        {
            ResourceType = isImage ? ResourceType.Image : ResourceType.Raw
        };

        var result = await _cloudinary.DestroyAsync(deletionParams);
        if (result.Error != null)
        {
            throw new Exception($"Cloudinary file deletion failed: {result.Error.Message}");
        }
    }
}