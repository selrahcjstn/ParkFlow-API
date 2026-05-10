using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.DTOs;

public record UpdateCorSubmissionRequest(
    string? AcademicTerm,
    string? CorDocumentUrl,
    CorVerificationStatus? VerificationStatus
);
