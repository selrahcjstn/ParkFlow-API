namespace ParkFlow.Application.Features.Users.DTOs;

public record ResetPasswordRequestDTO(string Email, string ResetToken, string NewPassword);
