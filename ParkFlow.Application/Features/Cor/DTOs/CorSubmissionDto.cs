using System;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.DTOs;

public record CorSubmissionDto(
    Guid Id,
    Guid UserAccountId,
    string AcademicTerm,
    string CorDocumentUrl,
    CorVerificationStatus VerificationStatus,
    string FullName,
    string Email,
    string VehiclePlate,
    string VehicleType,
    DateTime CreatedAt
);
