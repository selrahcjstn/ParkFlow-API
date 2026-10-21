using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
namespace ParkFlow.API.Security;

public class PrivateDocumentLinks(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("ParkFlow.PrivateDocuments.v1");
    private record Ticket(string Document, DateTime ExpiresAt);
    public static bool IsPrivate(string url) => url.StartsWith("private-uploads/", StringComparison.OrdinalIgnoreCase);
    public string Create(string document)
    {
        var token = protector.Protect(JsonSerializer.Serialize(new Ticket(document, DateTime.UtcNow.AddMinutes(15))));
        return "/api/files/document?url=" + Uri.EscapeDataString(document) + "&ticket=" + Uri.EscapeDataString(token)
            + (document.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "&format=pdf" : "&format=image");
    }
    public bool Valid(string document, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var ticket = JsonSerializer.Deserialize<Ticket>(protector.Unprotect(token));
            return ticket?.Document == document && ticket.ExpiresAt > DateTime.UtcNow;
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
        catch (JsonException) { return false; }
    }
}
