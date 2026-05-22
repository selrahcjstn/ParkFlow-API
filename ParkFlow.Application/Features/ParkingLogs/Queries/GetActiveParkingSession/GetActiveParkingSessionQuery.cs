// Query
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;

public record GetActiveParkingSessionQuery(
    int ParkingCapacity
) : IRequest<Result<IEnumerable<GetActiveParkingSessionResponse>>>;