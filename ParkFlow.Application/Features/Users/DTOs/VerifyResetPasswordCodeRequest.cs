namespace ParkFlow.Application.Features.Users.DTOs;

public record VerifyResetPasswordCodeRequest(string Email, string Code);
