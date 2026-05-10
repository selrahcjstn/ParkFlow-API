namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? ProfilePictureUrl
);
