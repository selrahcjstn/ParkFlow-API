namespace ParkFlow.Application.Features.Auth.DTOs;

public record UpdateMicrosoftIdentityRequest(
    string NewEmail,
    string NewExternalProviderId);
