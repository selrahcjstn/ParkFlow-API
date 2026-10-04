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
        private readonly ICacheService? _cacheService;

        public GetActiveSystemAnnouncementHandler(
            ISystemAnnouncementRepository repository,
            ICacheService? cacheService = null)
        {
            _repository = repository;
            _cacheService = cacheService;
        }

        public async Task<Result<SystemAnnouncementDto?>> Handle(GetActiveSystemAnnouncementQuery request, CancellationToken cancellationToken)
        {
            var cacheKey = CacheKeys.ActiveAnnouncement;

            if (_cacheService != null)
            {
                var cached = await _cacheService.GetAsync<SystemAnnouncementDto>(cacheKey, cancellationToken);
                if (cached != null)
                {
                    return Result<SystemAnnouncementDto?>.Success(cached, "Active system announcement retrieved.");
                }
            }

            var announcement = await _repository.GetActiveAsync();

            if (announcement == null)
            {
                return Result<SystemAnnouncementDto?>.Success(null, "No active system announcement.");
            }

            var dto = new SystemAnnouncementDto
            {
                Id = announcement.Id,
                Title = announcement.Title,
                Message = announcement.Message,
                IconType = announcement.IconType,
                IsActive = announcement.IsActive,
                CreatedAt = announcement.CreatedAt,
                UpdatedAt = announcement.UpdatedAt
            };

            if (_cacheService != null)
            {
                await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            }

            return Result<SystemAnnouncementDto?>.Success(dto, "Active system announcement retrieved.");
        }
    }
}
