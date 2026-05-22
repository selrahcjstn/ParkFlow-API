using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount;

public record GetSessionCountQuery(int ParkingCapacity)
	: IRequest<Result<SessionCountResponse>>;
