namespace ParkFlow.Application.Features.Auth.DTOs;

public record RegisterManualRequest(
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null,
    string? MiddleName = null,
    string? PhoneNumber = null,
    string? Role = null,
    string? Status = null,
    RegisterStudentDto? Student = null,
    RegisterPersonnelDto? Personnel = null,
    RegisterGuardDto? Guard = null);

public record RegisterStudentDto(
    string? StudentNumber,
    string? Course,
    string? Section,
    int? YearLevel);

public record RegisterPersonnelDto(
    string? IdCardNumber,
    string? Department);

public record RegisterGuardDto(
    int? AssignedGate);
