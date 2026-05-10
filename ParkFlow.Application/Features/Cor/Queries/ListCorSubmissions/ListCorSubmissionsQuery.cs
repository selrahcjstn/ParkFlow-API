using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;

namespace ParkFlow.Application.Features.Cor.Queries.ListCorSubmissions;

public record ListCorSubmissionsQuery() : IRequest<Result<IEnumerable<CorSubmissionDto>>>;
