namespace ParkFlow.Application.Features.Auth.DTOs;

public record LinkManualIdentityRequest(
    string Email,
    string Password);
