using System;
using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Cor.CreateCorSubmission
{
    public record CreateCorSubmissionCommand(
        Guid UserId,
        string AcademicTerm,
        string CorDocumentUrl,
        DateTime DateOfIncident
    ) : IRequest<Result<Guid>>;
}