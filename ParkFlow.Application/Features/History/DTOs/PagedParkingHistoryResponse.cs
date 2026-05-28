using System;
using System.Collections.Generic;

namespace ParkFlow.Application.Features.History.DTOs;

public class PagedParkingHistoryResponse
{
    public DateTime GeneratedAt { get; set; }
    public IEnumerable<ParkingHistoryResponse> Items { get; set; } = null!;
}
