namespace ParkFlow.Application.Features.Auth.DTOs;

public record LinkMicrosoftIdentityRequest(
    string ExternalProviderId,
    string Email,
    string? FirstName,
    string? LastName,
    string? DisplayName);
