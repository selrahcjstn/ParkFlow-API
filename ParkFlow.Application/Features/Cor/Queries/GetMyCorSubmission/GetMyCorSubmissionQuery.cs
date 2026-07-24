using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;

namespace ParkFlow.Application.Features.Cor.Queries.GetMyCorSubmission;

public record GetMyCorSubmissionQuery : IRequest<Result<CorSubmissionDto?>>;
