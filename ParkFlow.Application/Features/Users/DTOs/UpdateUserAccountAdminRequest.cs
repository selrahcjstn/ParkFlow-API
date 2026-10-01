namespace ParkFlow.Application.Features.Users.DTOs;

public record UpdateUserStudentRequest(
    string? StudentNumber,
    string? Course,
    string? Section,
    int? YearLevel
);

public record UpdateUserPersonnelRequest(
    string? IdCardNumber,
    string? Department
);

public record UpdateUserGuardRequest(
    int? AssignedGate
);

public record UpdateUserAccountAdminRequest(
    string? FirstName,
    string? LastName,
    string? MiddleName,
    string? Email,
    string? PhoneNumber,
    string? Role,
    string? Status,
    string? PhotoUrl,
    string? Password,
    UpdateUserStudentRequest? Student,
    UpdateUserPersonnelRequest? Personnel,
    UpdateUserGuardRequest? Guard
);
