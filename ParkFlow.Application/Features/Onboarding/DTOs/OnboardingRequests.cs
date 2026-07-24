using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.DTOs;

public record OnboardingProfileRequest(
    string PhoneNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? ProfilePictureUrl);

public record OnboardingStudentRequest(
    string StudentNumber,
    string Course,
    string Section,
    int YearLevel);

public record OnboardingPersonnelRequest(
    string IdCardNumber,
    string Department);

public record OnboardingVehicleRequest(
    string PlateNumber,
    string Brand,
    VehicleType VehicleType,
    string? Color = null,
    string? MotorPictureUrl = null,
    string? OrcrDocumentUrl = null);

public record OnboardingScheduleItem(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime);

public record OnboardingScheduleRequest(
    List<OnboardingScheduleItem> Items);

public record OnboardingCorRequest(
    string AcademicTerm,
    string CorDocumentUrl,
    string? OrcrDocumentUrl = null,
    string? MotorPictureUrl = null);
