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
    private readonly ISystemSettingsRepository _settingsRepository;

    public SystemSettingsController(
        ISignalRNotificationSender notificationSender,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingLogRepository parkingLogRepository,
        ISystemSettingsRepository settingsRepository,
        ICacheService? cacheService = null)
    {
        _notificationSender = notificationSender;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingLogRepository = parkingLogRepository;
        _cacheService = cacheService;
        _settingsRepository = settingsRepository;
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
        if (request.CarParking == null || request.MotorcycleParking == null)
            return BadRequest(Result<SystemSettingsDto>.Failure("Configure car and motorcycle parking settings.", ErrorCode.BadRequest));
        foreach (var (label, parking) in new[] { ("Car", request.CarParking), ("Motorcycle", request.MotorcycleParking) })
        {
            if (parking.TotalCapacity < 0 || parking.ReservationAllocationSlots < 0 || parking.ReservationAllocationSlots > parking.TotalCapacity)
                return BadRequest(Result<SystemSettingsDto>.Failure($"{label}: enter non-negative spaces. Reservation spaces cannot exceed this vehicle type's total capacity.", ErrorCode.BadRequest));
        }
        if ((long)request.CarParking.TotalCapacity + request.MotorcycleParking.TotalCapacity is < 1 or > int.MaxValue)
            return BadRequest(Result<SystemSettingsDto>.Failure("The campus must have at least one parking space.", ErrorCode.BadRequest));
        request.TotalCapacity = request.CarParking.TotalCapacity + request.MotorcycleParking.TotalCapacity;
        request.ReservationAllocationSlots = request.CarParking.ReservationAllocationSlots + request.MotorcycleParking.ReservationAllocationSlots;
        if (!PersonnelParkingPolicy.TryGetHours(request, out _, out _))
            return BadRequest(Result<SystemSettingsDto>.Failure("Enter valid faculty/staff hours with the start earlier than the end.", ErrorCode.BadRequest));
        var occupied = await _parkingLogRepository.GetOccupancyAsync();
        if (request.CarParking.TotalCapacity < occupied.Cars || request.MotorcycleParking.TotalCapacity < occupied.Motorcycles)
            return BadRequest(Result<SystemSettingsDto>.Failure($"Capacity cannot be lower than occupied spaces: {occupied.Cars} cars and {occupied.Motorcycles} motorcycles are currently inside.", ErrorCode.BadRequest));

        request.LastResetDate = SystemSettingsStore.Current.LastResetDate;
        await _settingsRepository.SaveAsync(request, HttpContext.RequestAborted);

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

        return Ok(Result<SystemSettingsDto>.Success(updated, "System settings updated successfully."));
    }

    [Authorize(Policy = "SuperAdminOnly")]
    [HttpPost("reset-student-schedules")]
    public async Task<ActionResult<Result<bool>>> ResetStudentSchedules(
        [FromServices] ParkFlow.Application.Features.Verification.VerificationService verificationService)
    {
        // 1. Record reset timestamp in settings
        var resetSettings = SystemSettingsStore.Current;
        resetSettings.LastResetDate = DateTime.UtcNow;
        await _settingsRepository.SaveAsync(resetSettings, HttpContext.RequestAborted);

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
