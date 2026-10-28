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
            _cloudinary.Api.Secure = true;
            _isCloudinaryConfigured = true;
        }
    }

    public async Task<(string SecureUrl, string PublicId)> UploadImageAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.", nameof(file));

        if (folder == "parkflow/licenses") return await SavePrivateLicenseAsync(file);

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
            catch (Exception)
            {
                Console.WriteLine("[CloudinaryService] Document storage operation failed.");
            }
        }

        // Fallback to local storage on server
        return await SaveLocalFileAsync(file, folder);
    }

    public async Task<(string SecureUrl, string PublicId)> UploadPdfAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null.", nameof(file));

        if (folder == "parkflow/licenses") return await SavePrivateLicenseAsync(file);

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
            catch (Exception)
            {
                Console.WriteLine("[CloudinaryService] Document storage operation failed.");
            }
        }

        // Fallback to local storage on server
        return await SaveLocalFileAsync(file, folder);
    }

    private static async Task<(string SecureUrl, string PublicId)> SavePrivateLicenseAsync(IFormFile file)
    {
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "private-uploads", "parkflow", "licenses");
        Directory.CreateDirectory(directory);
        await using var output = new FileStream(Path.Combine(directory, name), FileMode.CreateNew);
        await file.CopyToAsync(output);
        return ($"private-uploads/parkflow/licenses/{name}", name);
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

    private static readonly System.Net.Http.HttpClient _httpClient = new();

    public string? GetAuthenticatedDownloadUrl(string fileUrlOrPublicId, bool attachment = false)
    {
        if (string.IsNullOrWhiteSpace(fileUrlOrPublicId))
            return null;

        var publicId = ExtractPublicId(fileUrlOrPublicId);
        if (string.IsNullOrWhiteSpace(publicId))
            return null;

        if (_isCloudinaryConfigured && _cloudinary != null)
        {
            try
            {
                var resourceType = fileUrlOrPublicId.Contains("/raw/", StringComparison.OrdinalIgnoreCase) ||
                                   publicId.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? ResourceType.Raw
                    : ResourceType.Image;

                var downloadUrl = _cloudinary.DownloadPrivate(
                    publicId,
                    attachment: attachment,
                    format: "",
                    type: "upload",
                    resourceType: resourceType == ResourceType.Raw ? "raw" : "image"
                );
                return Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 ? downloadUrl : null;
            }
            catch (Exception)
            {
                Console.WriteLine("[CloudinaryService] Document storage operation failed.");
            }
        }

        return null;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> GetDocumentStreamAsync(string fileUrlOrPublicId, bool attachment = false)
    {
        if (string.IsNullOrWhiteSpace(fileUrlOrPublicId))
            return null;

        if (fileUrlOrPublicId.StartsWith("private-uploads/parkflow/licenses/", StringComparison.Ordinal))
        {
            var name = fileUrlOrPublicId["private-uploads/parkflow/licenses/".Length..];
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-f0-9]{32}\.(pdf|jpg|jpeg|png)$")) return null;
            var path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "private-uploads", "parkflow", "licenses", name);
            if (!File.Exists(path)) return null;
            var type = Path.GetExtension(name) switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
            return (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), type, name);
        }

        // 1. Check if it's a local file in wwwroot
        var trimmed = fileUrlOrPublicId.Trim().TrimStart('/');
        if (trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var publicRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads")) + Path.DirectorySeparatorChar;
            var localPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", trimmed.Replace('/', Path.DirectorySeparatorChar)));
            if (!localPath.StartsWith(publicRoot, StringComparison.OrdinalIgnoreCase)) return null;
            if (File.Exists(localPath))
            {
                var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var ext = Path.GetExtension(localPath).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };
                var fileName = Path.GetFileName(localPath);
                return (stream, contentType, fileName);
            }
        }

        // 2. Fetch authenticated Cloudinary stream
        var authUrl = GetAuthenticatedDownloadUrl(fileUrlOrPublicId, attachment);
        if (!string.IsNullOrWhiteSpace(authUrl))
        {
            try
            {
                var response = await _httpClient.GetAsync(authUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
                    var rawFileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"', '\'');
                    var fileName = !string.IsNullOrWhiteSpace(rawFileName)
                        ? rawFileName
                        : Path.GetFileName(new Uri(authUrl).AbsolutePath);

                    if (string.IsNullOrWhiteSpace(fileName) || fileName.EndsWith("download", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = "document.pdf";
                    }

                    return (stream, contentType, fileName);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[CloudinaryService] Document storage operation failed.");
            }
        }

        return null;
    }

    public static string ExtractPublicId(string urlOrPublicId)
    {
        if (string.IsNullOrWhiteSpace(urlOrPublicId)) return "";
        var trimmed = urlOrPublicId.Trim();

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var clean = trimmed.TrimStart('/');
            if (clean.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring("uploads/".Length);
            }
            return clean.TrimStart('/');
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath;
            var uploadIndex = path.IndexOf("/upload/", StringComparison.OrdinalIgnoreCase);
            if (uploadIndex >= 0)
            {
                var afterUpload = path.Substring(uploadIndex + "/upload/".Length);
                if (afterUpload.StartsWith("s--", StringComparison.OrdinalIgnoreCase))
                {
                    var nextSlash = afterUpload.IndexOf('/');
                    if (nextSlash >= 0)
                    {
                        afterUpload = afterUpload.Substring(nextSlash + 1);
                    }
                }
                if (System.Text.RegularExpressions.Regex.IsMatch(afterUpload, @"^v\d+/"))
                {
                    var nextSlash = afterUpload.IndexOf('/');
                    if (nextSlash >= 0)
                    {
                        afterUpload = afterUpload.Substring(nextSlash + 1);
                    }
                }
                return Uri.UnescapeDataString(afterUpload);
            }

            return Uri.UnescapeDataString(path.TrimStart('/'));
        }

        return trimmed;
    }
}
