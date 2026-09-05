using System;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.DTOs;

public record CorScheduleItemDto(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime
);

public record CorSubmissionDto(
    Guid Id,
    Guid UserAccountId,
    string AcademicTerm,
    string CorDocumentUrl,
    string? OrcrDocumentUrl,
    string? MotorPictureUrl,
    CorVerificationStatus VerificationStatus,
    string FullName,
    string Email,
    string VehiclePlate,
    string VehicleType,
    DateTime CreatedAt,
    List<CorScheduleItemDto>? Schedules = null
);
