namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UserProfileDto(
    Guid Id,
    Guid UserAccountId,
    string FirstName,
    string LastName,
    string? ProfilePictureUrl
);
