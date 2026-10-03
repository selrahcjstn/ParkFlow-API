using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;

public class UpdateCorSchedulesHandler : IRequestHandler<UpdateCorSchedulesCommand, Result<bool>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public UpdateCorSchedulesHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IUserAccountRepository? userAccountRepository = null,
        IVehicleRepository? vehicleRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _userAccountRepository = userAccountRepository;
        _vehicleRepository = vehicleRepository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<bool>> Handle(UpdateCorSchedulesCommand request, CancellationToken cancellationToken)
    {
        var submission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId);
        Guid targetUserAccountId = Guid.Empty;

        if (submission != null)
        {
            targetUserAccountId = submission.UserAccountId;
        }
        else
        {
            // 1. Fallback: check if the passed ID is actually a UserAccountId
            var latestByUser = await _corSubmissionRepository.GetLatestByUserIdAsync(request.CorSubmissionId);
            if (latestByUser != null)
            {
                submission = latestByUser;
                targetUserAccountId = submission.UserAccountId;
            }
            else if (_userAccountRepository != null && await _userAccountRepository.GetByIdAsync(request.CorSubmissionId) != null)
            {
                targetUserAccountId = request.CorSubmissionId;
            }
            else if (_vehicleRepository != null)
            {
                var vehicle = await _vehicleRepository.GetByIdAsync(request.CorSubmissionId);
                if (vehicle != null)
                {
                    targetUserAccountId = vehicle.OwnerId;
                    submission = await _corSubmissionRepository.GetLatestByUserIdAsync(targetUserAccountId);
                }
            }
        }

        if (submission == null && targetUserAccountId != Guid.Empty)
        {
            // Auto-create a pending CorSubmission so user always has a parent record for schedules
            submission = new CorSubmission(targetUserAccountId, "AY 2026-2027", "pending");
            await _corSubmissionRepository.AddCorSubmissionAsync(submission);
        }

        if (submission == null)
        {
            return Result<bool>.Failure("COR submission or user record not found.", ErrorCode.NotFound);
        }

        targetUserAccountId = submission.UserAccountId;

        var newSchedules = new List<ParkingSchedule>();
        if (request.Schedules != null && request.Schedules.Count > 0)
        {
            foreach (var item in request.Schedules)
            {
                if (TryParseFlexibleTime(item.StartTime, out var startTime) &&
                    TryParseFlexibleTime(item.EndTime, out var endTime))
                {
                    newSchedules.Add(new ParkingSchedule(
                        submission.Id,
                        (DayOfWeek)item.DayOfWeek,
                        startTime,
                        endTime));
                }
            }
        }

        // 1. Replace schedules on the primary submission
        await _parkingScheduleRepository.ReplaceSchedulesAsync(submission.Id, newSchedules);

        // 2. Synchronize across all other COR submissions belonging to this user account
        try
        {
            var allSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
            var userSubmissions = allSubmissions
                .Where(c => c.UserAccountId == targetUserAccountId && c.Id != submission.Id)
                .ToList();

            foreach (var userSub in userSubmissions)
            {
                var copySchedules = newSchedules.Select(s => new ParkingSchedule(userSub.Id, s.DayOfWeek, s.StartTime, s.EndTime)).ToList();
                await _parkingScheduleRepository.ReplaceSchedulesAsync(userSub.Id, copySchedules);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateCorSchedulesHandler] User submissions schedule sync note: {ex.Message}");
        }

        // 3. Realtime SignalR event dispatch to user and web clients
        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("ScheduleUpdated", new
                {
                    userId = targetUserAccountId,
                    submissionId = submission.Id
                });

                await _signalRNotificationSender.SendToUserAsync(targetUserAccountId.ToString(), "ScheduleUpdated", new
                {
                    userId = targetUserAccountId,
                    submissionId = submission.Id
                });

                await _signalRNotificationSender.SendToAllAsync("VerificationStatusUpdated", new
                {
                    userId = targetUserAccountId,
                    status = (int)submission.VerificationStatus
                });

                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "schedule",
                    userId = targetUserAccountId,
                    submissionId = submission.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateCorSchedulesHandler] SignalR dispatch notice: {ex.Message}");
            }
        }

        return Result<bool>.Success(true, "Parking schedules updated successfully.");
    }

    private static bool TryParseFlexibleTime(string input, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        // 1. Direct TimeSpan parse (e.g. "07:00:00", "07:00")
        if (TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out time))
            return true;

        // 2. Direct DateTime parse (handles "8:30 AM", "08:30", "19:00", etc.)
        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            time = dt.TimeOfDay;
            return true;
        }

        // 3. Fallback for colon split (e.g. "08:30:00:00")
        var parts = input.Split(':');
        if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
        {
            time = new TimeSpan(h % 24, m % 60, 0);
            return true;
        }

        return false;
    }
}
