using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession
{
    public record GetActiveParkingSessionQuery(int Limit = 100) : IRequest<Result<GetActiveParkingSessionResult>>;
}
