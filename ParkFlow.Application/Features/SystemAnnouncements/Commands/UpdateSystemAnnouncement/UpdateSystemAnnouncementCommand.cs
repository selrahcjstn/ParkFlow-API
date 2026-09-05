using System;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.SystemAnnouncements.DTOs;

namespace ParkFlow.Application.Features.SystemAnnouncements.Commands.UpdateSystemAnnouncement
{
    public record UpdateSystemAnnouncementCommand(
        Guid CreatedBy,
        string Title,
        string Message,
        string IconType = "caution",
        bool IsActive = true
    ) : IRequest<Result<SystemAnnouncementDto>>;
}
