namespace ParkFlow.Application.Features.Files.DTOs;

public class UploadFileResponse
{
    public string SecureUrl { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public Guid? SubmissionId { get; set; }
    public string? VerificationStatus { get; set; }
    public ParkFlow.Application.Features.Verification.CorSchedulePreview? CorPreview { get; set; }

    public UploadFileResponse() { }

    public UploadFileResponse(string secureUrl, string publicId)
    {
        SecureUrl = secureUrl;
        PublicId = publicId;
    }
}
