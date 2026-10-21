namespace ParkFlow.Application.Interfaces;
public interface IDriverDocumentRepository
{
    Task<bool> SubmitAsync(Guid userId, string? licenseUrl, string? photoUrl, CancellationToken ct);
}
