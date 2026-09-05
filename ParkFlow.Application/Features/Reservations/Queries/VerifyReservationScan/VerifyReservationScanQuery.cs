using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;

public record VerifyReservationScanQuery(string QrCode) : IRequest<Result<VerifyReservationScanResponse>>;
