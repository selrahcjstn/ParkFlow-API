namespace ParkFlow.Application.Features.Auth.DTOs;

public record VerifyEmailOtpRequest(string Email, string OtpCode);
