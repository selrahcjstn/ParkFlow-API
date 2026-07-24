using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace ParkFlow.API.Controllers;

[Route("api/system-settings")]
[ApiController]
public class SystemSettingsController : ControllerBase
{
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public SystemSettingsController(
        ISignalRNotificationSender notificationSender,
        ICorSubmissionRepository corSubmissionRepository)
    {
        _notificationSender = notificationSender;
        _corSubmissionRepository = corSubmissionRepository;
    }

    [HttpGet]
    public ActionResult<Result<SystemSettingsDto>> GetSettings()
    {
        var settings = SystemSettingsStore.Current;
        return Ok(Result<SystemSettingsDto>.Success(settings, "System settings retrieved."));
    }

    [HttpPut]
    public async Task<ActionResult<Result<SystemSettingsDto>>> UpdateSettings([FromBody] SystemSettingsDto request)
    {
        SystemSettingsStore.Update(
            request.ViolationRatePerHour,
            request.GracePeriodMinutes,
            request.AcademicYear,
            request.CurrentSemester);

        var updated = SystemSettingsStore.Current;

        // Broadcast rate update via SignalR
        try
        {
            await _notificationSender.SendToAllAsync("SystemSettingsUpdated", updated);
        }
        catch { }

        return Ok(Result<SystemSettingsDto>.Success(updated, "Violation rate per hour and system settings updated successfully."));
    }

    [HttpPost("reset-student-schedules")]
    public async Task<ActionResult<Result<bool>>> ResetStudentSchedules()
    {
        // 1. Record reset timestamp in settings
        SystemSettingsStore.RecordReset();

        // 2. Fetch all COR submissions & reset verification status so all students re-upload schedule & COR for new semester
        try
        {
            var submissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
            foreach (var sub in submissions)
            {
                sub.UpdateSubmission(null, null, CorVerificationStatus.Rejected); // Flag to require re-submission
                await _corSubmissionRepository.UpdateCorSubmissionAsync(sub);
            }
        }
        catch { }

        // 3. Notify mobile app clients via SignalR that new semester reset occurred
        try
        {
            await _notificationSender.SendToAllAsync("SemesterReset", new
            {
                Message = "New semester started. All student schedules & COR verifications require re-upload.",
                ResetAt = DateTime.UtcNow
            });
        }
        catch { }

        return Ok(Result<bool>.Success(true, "All student schedules and COR verification statuses have been reset for the new semester."));
    }
}
