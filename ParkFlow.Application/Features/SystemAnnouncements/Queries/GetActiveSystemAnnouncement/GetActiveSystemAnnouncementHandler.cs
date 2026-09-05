using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.SystemAnnouncements.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.SystemAnnouncements.Queries.GetActiveSystemAnnouncement
{
    public class GetActiveSystemAnnouncementHandler : IRequestHandler<GetActiveSystemAnnouncementQuery, Result<SystemAnnouncementDto?>>
    {
        private readonly ISystemAnnouncementRepository _repository;

        public GetActiveSystemAnnouncementHandler(ISystemAnnouncementRepository repository)
        {
            _repository = repository;
        }

        public async Task<Result<SystemAnnouncementDto?>> Handle(GetActiveSystemAnnouncementQuery request, CancellationToken cancellationToken)
        {
            var announcement = await _repository.GetActiveAsync();

            if (announcement == null)
            {
                return Result<SystemAnnouncementDto?>.Success(null, "No active system announcement.");
            }

            var dto = new SystemAnnouncementDto
            {
                Id = announcement.Id,
                Message = announcement.Message,
                IconType = announcement.IconType,
                IsActive = announcement.IsActive,
                CreatedAt = announcement.CreatedAt,
                UpdatedAt = announcement.UpdatedAt
            };

            return Result<SystemAnnouncementDto?>.Success(dto, "Active system announcement retrieved.");
        }
    }
}
