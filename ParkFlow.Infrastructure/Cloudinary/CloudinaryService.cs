using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ParkFlow.Application.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ParkFlow.Infrastructure.Cloudinary;

public class CloudinaryService : ICloudinaryService
{
    private readonly CloudinaryDotNet.Cloudinary? _cloudinary;
    private readonly bool _isCloudinaryConfigured;

    public CloudinaryService(IOptions<CloudinarySettings> config)
    {
        if (config.Value != null &&
            !string.IsNullOrWhiteSpace(config.Value.CloudName) &&
            !string.IsNullOrWhiteSpace(config.Value.ApiKey) &&
            !string.IsNullOrWhiteSpace(config.Value.ApiSecret))
        {
            var account = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );

            _cloudinary = new CloudinaryDotNet.Cloudinary(account);
            _isCloudinaryConfigured = true;
        }
    }

    public async Task<(string SecureUrl, string PublicId)> UploadImageAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.", nameof(file));

        if (_isCloudinaryConfigured && _cloudinary != null)
        {
            try
            {
                using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder
                };

                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result != null && result.Error == null && result.SecureUrl != null)
                {
                    return (result.SecureUrl.ToString(), result.PublicId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudinaryService] Cloudinary image upload warning, falling back to local storage: {ex.Message}");
            }
        }

        // Fallback to local storage on server
        return await SaveLocalFileAsync(file, folder);
    }

    public async Task<(string SecureUrl, string PublicId)> UploadPdfAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.", nameof(file));

        if (_isCloudinaryConfigured && _cloudinary != null)
        {
            try
            {
                using var stream = file.OpenReadStream();

                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder
                };

                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result != null && result.Error == null && result.SecureUrl != null)
                {
                    return (result.SecureUrl.ToString(), result.PublicId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudinaryService] Cloudinary PDF upload warning, falling back to local storage: {ex.Message}");
            }
        }

        // Fallback to local storage on server
        return await SaveLocalFileAsync(file, folder);
    }

    private async Task<(string SecureUrl, string PublicId)> SaveLocalFileAsync(IFormFile file, string folder)
    {
        var cleanFolder = folder.Replace('/', Path.DirectorySeparatorChar);
        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", cleanFolder);
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        var relativeUrl = $"uploads/{folder.Replace('\\', '/')}/{uniqueFileName}";
        return (relativeUrl, uniqueFileName);
    }

    public async Task DeleteFileAsync(string publicId, bool isImage = true)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return;

        if (_isCloudinaryConfigured && _cloudinary != null)
        {
            try
            {
                var deletionParams = new DeletionParams(publicId)
                {
                    ResourceType = isImage ? ResourceType.Image : ResourceType.Raw
                };

                await _cloudinary.DestroyAsync(deletionParams);
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }
}