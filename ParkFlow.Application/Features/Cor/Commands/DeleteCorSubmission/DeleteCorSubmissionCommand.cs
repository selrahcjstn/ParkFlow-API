using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Cor.Commands.DeleteCorSubmission;

public record DeleteCorSubmissionCommand(Guid CorSubmissionId) : IRequest<Result<Guid>>;
