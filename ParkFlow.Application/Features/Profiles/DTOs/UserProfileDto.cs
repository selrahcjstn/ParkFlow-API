namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UserProfileDto(
    Guid Id,
    Guid UserAccountId,
    string IdCardNumber,
    string FirstName,
    string LastName,
    string? ProfilePictureUrl,
    string? Course,
    string? Section,
    int? YearLevel,
    string? Office,
    string? Department
);
