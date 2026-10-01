namespace ParkFlow.Application.Features.Users.DTOs;

public record SendTempPasswordRequestDTO(
    string? Email,
    string? TargetEmail,
    string TemporaryPassword
);
