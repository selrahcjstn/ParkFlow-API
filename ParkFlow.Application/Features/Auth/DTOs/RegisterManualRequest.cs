namespace ParkFlow.Application.Features.Auth.DTOs;

public record RegisterManualRequest(
    string Email,
    string Password);
