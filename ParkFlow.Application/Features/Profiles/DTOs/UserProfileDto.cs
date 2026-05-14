using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UserProfileDto(
    Guid Id,
    Guid UserAccountId,
    string PhoneNumber,
    string IdCardNumber,
    string FirstName,
    string LastName,
    string? ProfilePictureUrl
);
