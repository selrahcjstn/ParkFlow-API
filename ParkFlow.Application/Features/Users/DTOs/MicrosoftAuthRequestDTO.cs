namespace ParkFlow.Application.Features.Users.DTOs;

public class MicrosoftAuthRequestDTO
{
    public string ExternalProviderId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
}
