using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;

public class UpdateCorSchedulesHandler : IRequestHandler<UpdateCorSchedulesCommand, Result<bool>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public UpdateCorSchedulesHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<bool>> Handle(UpdateCorSchedulesCommand request, CancellationToken cancellationToken)
    {
        var submission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId);
        if (submission == null)
        {
            // Fallback: check if the passed ID is actually a UserAccountId
            var latest = await _corSubmissionRepository.GetLatestByUserIdAsync(request.CorSubmissionId);
            if (latest != null)
            {
                submission = latest;
            }
            else
            {
                return Result<bool>.Failure("COR submission record not found.", ErrorCode.NotFound);
            }
        }

        var newSchedules = new List<ParkingSchedule>();
        if (request.Schedules != null && request.Schedules.Count > 0)
        {
            foreach (var item in request.Schedules)
            {
                if (TimeSpan.TryParse(item.StartTime, out var startTime) &&
                    TimeSpan.TryParse(item.EndTime, out var endTime))
                {
                    newSchedules.Add(new ParkingSchedule(
                        submission.Id,
                        (DayOfWeek)item.DayOfWeek,
                        startTime,
                        endTime));
                }
            }
        }

        await _parkingScheduleRepository.ReplaceSchedulesAsync(submission.Id, newSchedules);

        // Realtime SignalR event dispatch to user and web clients
        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("ScheduleUpdated", new
                {
                    userId = submission.UserAccountId,
                    submissionId = submission.Id
                });

                await _signalRNotificationSender.SendToAllAsync("VerificationStatusUpdated", new
                {
                    userId = submission.UserAccountId,
                    status = (int)submission.VerificationStatus
                });

                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "schedule",
                    userId = submission.UserAccountId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateCorSchedulesHandler] SignalR dispatch notice: {ex.Message}");
            }
        }

        return Result<bool>.Success(true, "Parking schedules updated successfully.");
    }
}
