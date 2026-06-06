namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UpdateUserProfileRequest(
    string? IdCardNumber,
    string? FirstName,
    string? LastName,
    string? MiddleName,
    string? ProfilePictureUrl,
    string? Course,
    string? Section,
    int? YearLevel,
    string? Office
);
