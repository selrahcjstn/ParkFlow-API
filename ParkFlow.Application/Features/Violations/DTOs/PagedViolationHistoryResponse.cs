namespace ParkFlow.Application.Features.Violations.DTOs;

public class PagedViolationHistoryResponse
{
    public DateTime GeneratedAt { get; set; }
    public IEnumerable<ViolationHistoryResponse> Items { get; set; } = [];
}
