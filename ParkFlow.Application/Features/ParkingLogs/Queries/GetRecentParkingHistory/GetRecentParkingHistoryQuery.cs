using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;

public record GetRecentParkingHistoryQuery(int Limit = 20) : IRequest<Result<IEnumerable<ParkingLogHistoryDto>>>;