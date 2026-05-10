using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;

public record UpdateCorSubmissionCommand(
    Guid CorSubmissionId,
    string? AcademicTerm,
    string? CorDocumentUrl,
    CorVerificationStatus? VerificationStatus
) : IRequest<Result<Guid>>;
