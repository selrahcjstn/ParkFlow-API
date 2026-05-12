using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission
{
    public record CreateCorSubmissionCommand(
        Guid UserId,
        string AcademicTerm,
        string CorDocumentUrl
    ) : IRequest<Result<Guid>>;
}