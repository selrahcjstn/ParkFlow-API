namespace ParkFlow.Application.Features.Files.DTOs;

public class UploadFileResponse
{
    public string SecureUrl { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;

    public UploadFileResponse() { }

    public UploadFileResponse(string secureUrl, string publicId)
    {
        SecureUrl = secureUrl;
        PublicId = publicId;
    }
}
