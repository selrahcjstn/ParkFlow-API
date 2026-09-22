using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;

public record ValidateCorSubmissionCommand(
    Guid CorSubmissionId,
    CorVerificationStatus VerificationStatus,
    string? RejectionReason = null
) : IRequest<Result<Guid>>;
