using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;

public record VerifyStudentScanQuery(
    string? QrContent = null,
    string? StudentNumber = null,
    string? FullName = null,
    string? Program = null
) : IRequest<Result<VerifyStudentScanResponse>>;
