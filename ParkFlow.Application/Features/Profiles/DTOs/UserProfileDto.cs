using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UserProfileDto(
    Guid Id,
    Guid UserAccountId,
    string PhoneNumber,
    Roles Role,
    string IdCardNumber,
    string FirstName,
    string LastName,
    string? ProfilePictureUrl,
    string? Course,
    string? Section,
    int? YearLevel,
    string? Office
);
