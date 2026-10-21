namespace ParkFlow.Application.Interfaces;

public record DocumentText(string Text, string Outcome);
public interface ICorDocumentReader
{
    Task ValidateAsync(Stream stream, string fileName, CancellationToken ct);
    Task<DocumentText> ReadAsync(Stream stream, string fileName, CancellationToken ct);
    // Validate once and return only a digital text layer. Native OCR stays off
    // the upload request; null means the existing worker must process the file.
    async Task<DocumentText?> ReadTextLayerAsync(Stream stream, string fileName, CancellationToken ct)
    { await ValidateAsync(stream, fileName, ct); return null; }
    bool IsStoredDocument(string url);
}

public class DocumentValidationException(string message) : Exception(message);

// Providers must use authorized university/ownership records and bind evidence to the
// exact subject fingerprint. OCR, email domains and formatted IDs are not evidence.
public record TrustedVerificationEvidence(bool Succeeded, string? Reference, string Fingerprint);
public interface IInstitutionalVerificationProvider
{
    Task<TrustedVerificationEvidence> VerifyAsync(VerificationSubject subject, CancellationToken ct);
}
