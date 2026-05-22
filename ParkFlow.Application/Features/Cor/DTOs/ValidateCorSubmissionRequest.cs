using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.DTOs;

public record ValidateCorSubmissionRequest(
    CorVerificationStatus VerificationStatus
);
