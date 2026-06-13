using System;
using System.Collections.Generic;

namespace ParkFlow.Application.Features.Users.DTOs;

public record UserVehicleDto(
    string PlateNumber,
    string Brand,
    string VehicleType,
    bool IsPrimary
);

public record UserStudentDto(
    string StudentNumber,
    string Course,
    string Section,
    int YearLevel
);

public record UserPersonnelDto(
    string IdCardNumber,
    string Department
);

public record UserGuardDto(
    int AssignedGate
);

public record UserWithDetailsDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    string Email,
    string PhoneNumber,
    string Status,
    string AuthProvider,
    string Role,
    DateTime CreatedAt,
    string? ProfilePictureUrl,
    UserStudentDto? Student,
    UserPersonnelDto? Personnel,
    UserGuardDto? Guard,
    IEnumerable<UserVehicleDto> Vehicles
);
