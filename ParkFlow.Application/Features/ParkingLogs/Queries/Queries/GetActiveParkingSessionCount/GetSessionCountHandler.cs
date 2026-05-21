using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetSessionCount;

public record GetSessionCountQuery(Guid OwnerId) : IRequest<Result<IEnumerable<VehicleDto>>>;

