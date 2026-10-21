using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ParkFlow.Application.Features.ParkingLogs.Services;
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
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly ICacheService? _cacheService;

    public SystemSettingsController(
        ISignalRNotificationSender notificationSender,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingLogRepository parkingLogRepository,
        ICacheService? cacheService = null)
    {
        _notificationSender = notificationSender;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingLogRepository = parkingLogRepository;
        _cacheService = cacheService;
    }

    [HttpGet]
    public ActionResult<Result<SystemSettingsDto>> GetSettings()
    {
        var settings = SystemSettingsStore.Current;
        return Ok(Result<SystemSettingsDto>.Success(settings, "System settings retrieved."));
    }

    [HttpPut]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<Result<SystemSettingsDto>>> UpdateSettings([FromBody] SystemSettingsDto request)
    {
        var isSuperAdmin = User.IsInRole("SuperAdmin") || User.HasClaim("role", "SuperAdmin")
            || User.HasClaim("profile_type", "superadmin");
        if (!isSuperAdmin) return Forbid();
        if (!ParkFlow.Application.Features.Verification.CorAcademicTerm.IsValid(request.AcademicYear, request.CurrentSemester))
            return BadRequest(Result<SystemSettingsDto>.Failure("Enter a valid school year (YYYY-YYYY) and semester (1st Semester, 2nd Semester or Summer).", ErrorCode.BadRequest));
        if (request.TotalCapacity < 1 || request.ReservationAllocationSlots < 0 || request.ReservationAllocationSlots > request.TotalCapacity)
            return BadRequest(Result<SystemSettingsDto>.Failure(
                "Total parking capacity must be at least 1. Reservation spaces must be between 0 and the total parking capacity.", ErrorCode.BadRequest));
        if (!PersonnelParkingPolicy.TryGetHours(request, out _, out _) || request.ViolationRatePerHour < 0)
            return BadRequest(Result<SystemSettingsDto>.Failure(
                "Enter valid faculty/staff hours with the start earlier than the end, and a non-negative hourly rate.",
                ErrorCode.BadRequest));
        if (request.TotalCapacity > 0)
        {
            var activeLogs = await _parkingLogRepository.GetActiveParkingLogsAsync(10000);
            var activeCount = activeLogs.Count;
            if (request.TotalCapacity < activeCount)
            {
                return BadRequest(Result<SystemSettingsDto>.Failure(
                    $"Cannot set total capacity to {request.TotalCapacity} slots. There are currently {activeCount} active vehicles parked on campus. Capacity cannot be lower than the active parked count.",
                    ErrorCode.BadRequest));
            }
        }

        SystemSettingsStore.Update(
            request.ViolationRatePerHour,
            request.GracePeriodMinutes,
            request.AcademicYear,
            request.CurrentSemester,
            request.MaxParkingHours,
            request.TotalCapacity,
            request.MaxVehiclesPerUser,
            request.MaintenanceMode,
            request.RfidInstantScanEnabled,
            request.AutoApproveVerification,
            request.FeeCalculationMode,
            request.BaseFee,
            request.IsGracePeriodEnabled,
            request.IsEarlyParkingAllowed,
            request.EarlyParkingMinutes,
            request.PersonnelFreeParkingStart,
            request.PersonnelFreeParkingEnd,
            request.ReservationAllocationSlots);

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix);
        }
        var updated = SystemSettingsStore.Current;

        // Broadcast rate and capacity update via SignalR
        try
        {
            await _notificationSender.SendToAllAsync("SystemSettingsUpdated", updated);
            await _notificationSender.SendToAllAsync("ParkingOccupancyUpdated", new
            {
                totalCapacity = updated.TotalCapacity,
                maxParkingHours = updated.MaxParkingHours,
                action = "CapacityUpdated"
            });
        }
        catch { }

        return Ok(Result<SystemSettingsDto>.Success(updated, "Violation rate per hour and system settings updated successfully."));
    }

    [Authorize(Policy = "SuperAdminOnly")]
    [HttpPost("reset-student-schedules")]
    public async Task<ActionResult<Result<bool>>> ResetStudentSchedules(
        [FromServices] ParkFlow.Application.Features.Verification.VerificationService verificationService)
    {
        // 1. Record reset timestamp in settings
        SystemSettingsStore.RecordReset();

        // 2. Fetch all COR submissions & reset verification status so all students re-upload schedule & COR for new semester
        try
        {
            var submissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
            foreach (var sub in submissions)
            {
                // An employee ID is not a semester schedule and must remain approved.
                if (PersonnelParkingPolicy.AppliesTo(sub.UserAccount?.UserProfile?.Personnel)) continue;
                var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
                const string reason = "Semester cycle reset. Please upload your Certificate of Registration (COR) and class schedule for the new semester.";
                if (!await verificationService.ReviewSubjectAsync(ParkFlow.Domain.Entities.VerificationType.Cor, sub.Id,
                    ParkFlow.Domain.Entities.VerificationState.Rejected, reason, actor, HttpContext.RequestAborted) ||
                    !await verificationService.ReviewSubjectAsync(ParkFlow.Domain.Entities.VerificationType.Schedule, sub.Id,
                    ParkFlow.Domain.Entities.VerificationState.Rejected, reason, actor, HttpContext.RequestAborted))
                    return Conflict(Result<bool>.Failure("A submission changed during the semester reset. Refresh and retry to reset the remaining records.", ErrorCode.Conflict));
            }
        }
        catch (Exception)
        {
            return StatusCode(503, Result<bool>.Failure("Semester reset could not finish. Refresh the approval records and retry.", ErrorCode.BadRequest));
        }

        // 3. Notify mobile app clients and admin dashboards via SignalR that new semester reset occurred
        try
        {
            var resetTime = DateTime.UtcNow;
            await _notificationSender.SendToAllAsync("SemesterReset", new
            {
                Message = "New semester started. All student schedules & COR verifications require re-upload.",
                ResetAt = resetTime
            });

            await _notificationSender.SendToAllAsync("ApprovalListUpdated", new
            {
                type = "schedule",
                action = "SemesterReset",
                resetAt = resetTime
            });

            await _notificationSender.SendToAllAsync("VerificationStatusChanged", new
            {
                type = "SemesterReset",
                status = "Rejected",
                resetAt = resetTime
            });

            await _notificationSender.SendToAllAsync("SystemSettingsUpdated", SystemSettingsStore.Current);
        }
        catch { }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SchedulePrefix);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.UserPrefix);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix);
        }

        return Ok(Result<bool>.Success(true, "All student schedules and COR verification statuses have been reset for the new semester."));
    }
}
