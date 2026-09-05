using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.SystemAnnouncements.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.SystemAnnouncements.Commands.UpdateSystemAnnouncement
{
    public class UpdateSystemAnnouncementHandler : IRequestHandler<UpdateSystemAnnouncementCommand, Result<SystemAnnouncementDto>>
    {
        private readonly ISystemAnnouncementRepository _repository;

        public UpdateSystemAnnouncementHandler(ISystemAnnouncementRepository repository)
        {
            _repository = repository;
        }

        public async Task<Result<SystemAnnouncementDto>> Handle(UpdateSystemAnnouncementCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Result<SystemAnnouncementDto>.Failure("Announcement message is required.", ErrorCode.BadRequest);
            }

            if (!string.IsNullOrWhiteSpace(request.Title) && request.Title.Trim().Length > 60)
            {
                return Result<SystemAnnouncementDto>.Failure("Announcement title/header cannot exceed 60 characters.", ErrorCode.BadRequest);
            }

            var existing = await _repository.GetActiveAsync();

            if (existing == null)
            {
                existing = new SystemAnnouncement(
                    request.CreatedBy,
                    request.Title.Trim(),
                    request.Message.Trim(),
                    request.IconType,
                    request.IsActive
                );
                await _repository.AddAsync(existing);
            }
            else
            {
                existing.Update(
                    request.Title.Trim(),
                    request.Message.Trim(),
                    request.IconType,
                    request.IsActive
                );
                await _repository.UpdateAsync(existing);
            }

            var dto = new SystemAnnouncementDto
            {
                Id = existing.Id,
                Title = existing.Title,
                Message = existing.Message,
                IconType = existing.IconType,
                IsActive = existing.IsActive,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = existing.UpdatedAt
            };

            return Result<SystemAnnouncementDto>.Success(dto, "System announcement updated successfully.");
        }
    }
}
