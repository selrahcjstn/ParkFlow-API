using System;
using System.Collections.Generic;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.History.DTOs;

namespace ParkFlow.Application.Features.History.Queries;

public record GetParkingHistoryQuery(Guid UserId, int PageNumber = 1, int PageSize = 15) : IRequest<Result<PagedParkingHistoryResponse>>;
