using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Profiles.DTOs;

public record UserProfileDto(
    Guid Id,
    Guid UserAccountId,
    string PhoneNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? ProfilePictureUrl,
    OnboardingStep OnboardingStep,
    string? StudentNumber = null,
    string? EmployeeIdNumber = null,
    string? Course = null,
    int? YearLevel = null,
    string? Section = null,
    string? Department = null,
    CorVerificationStatus? CorVerificationStatus = null
);
