using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.SystemAnnouncements.DTOs;

namespace ParkFlow.Application.Features.SystemAnnouncements.Queries.GetActiveSystemAnnouncement
{
    public record GetActiveSystemAnnouncementQuery : IRequest<Result<SystemAnnouncementDto?>>;
}
