using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetTodayParkingLogs;

public record GetTodayParkingLogsQuery(int Limit = 100) : IRequest<Result<ParkingLogsTodayResponse>>;