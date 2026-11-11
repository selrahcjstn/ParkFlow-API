using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Infrastructure.Services;

public class OverstayNotificationService : IOverstayNotificationService
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly INotificationService _notificationService;
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly IEmailService? _emailService;
    private readonly IViolationRepository? _violationRepository;
    private readonly ILogger<OverstayNotificationService> _logger;

    // In-memory deduplication cache: ReferenceCode -> 1
    private static readonly ConcurrentDictionary<string, byte> _sentReferenceCodes = new();

    public OverstayNotificationService(
        IParkingLogRepository parkingLogRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        INotificationService notificationService,
        IUserNotificationRepository userNotificationRepository,
        ILogger<OverstayNotificationService> logger,
        IParkingReservationRepository? reservationRepository = null,
        IEmailService? emailService = null,
        IViolationRepository? violationRepository = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _notificationService = notificationService;
        _userNotificationRepository = userNotificationRepository;
        _logger = logger;
        _reservationRepository = reservationRepository;
        _emailService = emailService;
        _violationRepository = violationRepository;
    }

    public async Task<int> CheckAndSendOverstayNotificationsAsync(DateTime? checkTimeUtc = null, CancellationToken ct = default)
    {
        var nowUtc = checkTimeUtc ?? DateTime.UtcNow;
        var sysSettings = SystemSettingsStore.Current;

        IReadOnlyList<ParkingLog> logs;
        try
        {
            logs = await _parkingLogRepository.GetActiveParkingLogsAsync(int.MaxValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active parking logs during overstay check.");
            return 0;
        }

        var activeLogs = logs
            .Where(x => x.EntryTime != default && x.Status == ParkingStatus.Parked && x.ExitTime == null && x.Vehicle != null)
            .ToList();

        if (activeLogs.Count == 0)
        {
            return 0;
        }

        // Clean up cached keys for logs that are no longer active
        var activeLogIdStrings = activeLogs.Select(l => l.Id.ToString()).ToHashSet();
        foreach (var key in _sentReferenceCodes.Keys)
        {
            // Key format: OVERSTAY-INITIAL-{logId} or OVERSTAY-REMINDER-{logId}-H{hour}
            var parts = key.Split('-');
            if (parts.Length >= 3)
            {
                var keyLogId = parts[2];
                if (!activeLogIdStrings.Contains(keyLogId))
                {
                    _sentReferenceCodes.TryRemove(key, out _);
                }
            }
        }

        var ownerIds = activeLogs
            .Select(log => log.Vehicle.OwnerId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var corSubmissions = (await _corSubmissionRepository.GetByUserIdsAsync(ownerIds)).ToList();
        var schedulesBySubmission = (await _parkingScheduleRepository.GetBySubmissionIdsAsync(corSubmissions.Select(cor => cor.Id)))
            .ToLookup(schedule => schedule.SubmissionId);

        var reservationsByOwner = (_reservationRepository == null
            ? new List<ParkingReservation>()
            : (await _reservationRepository.GetByUserIdsAsync(ownerIds)).ToList())
            .ToLookup(reservation => reservation.UserId);

        var notificationsSent = 0;

        foreach (var log in activeLogs)
        {
            if (ct.IsCancellationRequested) break;

            var vehicle = log.Vehicle;
            var owner = vehicle.Owner;
            if (owner == null || owner.Id == Guid.Empty)
            {
                continue;
            }

            var timing = ActiveSessionTiming.ResolveActive(
                log, nowUtc, sysSettings, corSubmissions, schedulesBySubmission, reservationsByOwner[owner.Id]);

            if (timing.MaximumExitTime == null)
            {
                continue;
            }

            var isOverstaying = timing.OverstayHours > 0 || nowUtc >= timing.MaximumExitTime.Value;
            var isGracePeriodWindow = !isOverstaying && nowUtc >= timing.MaximumExitTime.Value.AddMinutes(-30);

            if (!isOverstaying && !isGracePeriodWindow)
            {
                continue;
            }

            var ownerId = owner.Id;
            var ownerProfile = owner.UserProfile;
            var ownerEmail = owner.PrimaryEmail
                ?? owner.AuthIdentities?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Email))?.Email;

            var driverName = !string.IsNullOrWhiteSpace(ownerProfile?.FirstName)
                ? $"{ownerProfile.FirstName} {ownerProfile.LastName}".Trim()
                : "Driver";

            var plateNumber = !string.IsNullOrWhiteSpace(vehicle.PlateNumber) ? vehicle.PlateNumber : vehicle.Brand;
            var vehicleBrand = vehicle.Brand;
            var vehicleType = vehicle.VehicleType.ToString();

            var deadlinePhilippines = ParkingTimeHelper.ConvertUtcToPhilippinesTime(timing.MaximumExitTime.Value);
            var deadlineStr = deadlinePhilippines.ToString("hh:mm tt");
            var entryPhilippines = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
            var entryStr = entryPhilippines.ToString("hh:mm tt");

            // ── 0. Grace Period Trigger (30 Minutes Before Deadline: In-App + 1 Email) ──
            if (isGracePeriodWindow)
            {
                var graceRefCode = $"GRACE-PERIOD-{log.Id}";
                if (!await IsReferenceCodeProcessedAsync(ownerId, graceRefCode))
                {
                    _sentReferenceCodes.TryAdd(graceRefCode, 1);

                    try
                    {
                        await _notificationService.CreateAndSendNotificationAsync(
                            userAccountId: ownerId,
                            title: "Parking Grace Period Alert",
                            body: $"Your vehicle [{plateNumber}] has entered its 30-minute grace period. Please exit by {deadlineStr} to avoid parking overstay violations.",
                            type: "reminder",
                            subtitle: $"{plateNumber} • 30-Min Grace Period",
                            referenceCode: graceRefCode,
                            vehiclePlate: plateNumber,
                            actionRoute: "(tabs)/home",
                            actionText: "View Active Session",
                            priority: "high",
                            issuer: "Security Office",
                            driverName: driverName,
                            vehicleBrand: vehicleBrand
                        );

                        notificationsSent++;
                        _logger.LogInformation("Sent 30-minute grace period alert for vehicle {Plate} (Log {LogId})", plateNumber, log.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send grace period in-app notification for log {LogId}", log.Id);
                    }

                    // Send Grace Period Email
                    if (_emailService != null && !string.IsNullOrWhiteSpace(ownerEmail))
                    {
                        try
                        {
                            var emailSubject = $"⏳ ParkFlow - 30-Minute Parking Grace Period Notice [{plateNumber}]";
                            var emailHtml = BuildGracePeriodEmailHtml(driverName, plateNumber, vehicleBrand, vehicleType, entryStr, deadlineStr);
                            await _emailService.SendEmailAsync(ownerEmail, emailSubject, emailHtml);
                            _logger.LogInformation("Sent grace period email to {Email} for vehicle {Plate}", ownerEmail, plateNumber);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send grace period email to {Email} for vehicle {Plate}", ownerEmail, plateNumber);
                        }
                    }
                }
            }

            // ── 1. Initial Overstay Alert (In-App + 1 Email) ──
            if (isOverstaying)
            {
                if (_violationRepository != null)
                {
                    try
                    {
                        var userReservation = reservationsByOwner[owner.Id].FirstOrDefault(r => r.VehicleId == vehicle.Id && r.Status == ReservationStatus.Approved);
                        var fee = ParkingCharges.Amount(vehicle.VehicleType, log.EntryMethod, ownerProfile?.Personnel,
                            ownerProfile?.Student != null, userReservation, timing.OverstayHours);

                        if (fee > 0)
                        {
                            var existing = await _violationRepository.GetByLogIdAsync(log.Id);
                            if (existing == null)
                            {
                                var newViolation = new Violation(log.Id, fee, ViolationType.Overstay);
                                await _violationRepository.AddAsync(newViolation);
                                _logger.LogInformation("Recorded overstay violation {Ref} for log {LogId} (Amount: {Fee})", newViolation.ReferenceNumber, log.Id, fee);
                            }
                            else if (existing.SettlementStatus != SettlementStatus.Settled && existing.PenaltyFee != fee)
                            {
                                existing.UpdatePenaltyFee(fee);
                                await _violationRepository.UpdateAsync(existing);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record or update overstay violation for log {LogId}", log.Id);
                    }
                }

                var initialRefCode = $"OVERSTAY-INITIAL-{log.Id}";
            if (!await IsReferenceCodeProcessedAsync(ownerId, initialRefCode))
            {
                _sentReferenceCodes.TryAdd(initialRefCode, 1);

                try
                {
                    await _notificationService.CreateAndSendNotificationAsync(
                        userAccountId: ownerId,
                        title: "Parking Overstay Alert",
                        body: $"Your vehicle [{plateNumber}] has exceeded the allowed parking deadline ({deadlineStr}). Please exit immediately to avoid parking violations.",
                        type: "overdue",
                        subtitle: $"{plateNumber} • Overstay Notice",
                        referenceCode: initialRefCode,
                        vehiclePlate: plateNumber,
                        actionRoute: "(tabs)/home",
                        actionText: "View Active Session",
                        priority: "high",
                        issuer: "Security Office",
                        driverName: driverName,
                        vehicleBrand: vehicleBrand
                    );

                    notificationsSent++;
                    _logger.LogInformation("Sent initial in-app overstay alert for vehicle {Plate} (Log {LogId})", plateNumber, log.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send initial in-app notification for log {LogId}", log.Id);
                }

                // Send 1 Overstay Email
                if (_emailService != null && !string.IsNullOrWhiteSpace(ownerEmail))
                {
                    try
                    {
                        var emailSubject = $"⚠️ ParkFlow - Parking Overstay Notice [{plateNumber}]";
                        var emailHtml = BuildOverstayEmailHtml(driverName, plateNumber, vehicleBrand, vehicleType, entryStr, deadlineStr);
                        await _emailService.SendEmailAsync(ownerEmail, emailSubject, emailHtml);
                        _logger.LogInformation("Sent initial overstay email to {Email} for vehicle {Plate}", ownerEmail, plateNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send overstay email to {Email} for vehicle {Plate}", ownerEmail, plateNumber);
                    }
                }
            }

            // ── 2. Hourly Overstay Reminder (In-App Only) ──
            var elapsedHours = (int)Math.Floor(timing.OverstayHours);
            if (elapsedHours >= 1)
            {
                for (var hour = 1; hour <= elapsedHours; hour++)
                {
                    var hourlyRefCode = $"OVERSTAY-REMINDER-{log.Id}-H{hour}";
                    if (!await IsReferenceCodeProcessedAsync(ownerId, hourlyRefCode))
                    {
                        _sentReferenceCodes.TryAdd(hourlyRefCode, 1);

                        try
                        {
                            await _notificationService.CreateAndSendNotificationAsync(
                                userAccountId: ownerId,
                                title: "Parking Overstay Reminder",
                                body: $"Reminder: Your vehicle [{plateNumber}] has been overstaying for {hour} hour{(hour == 1 ? "" : "s")}. Please proceed to your vehicle and exit the campus.",
                                type: "reminder",
                                subtitle: $"{hour} Hour{(hour == 1 ? "" : "s")} Overstay",
                                referenceCode: hourlyRefCode,
                                vehiclePlate: plateNumber,
                                actionRoute: "(tabs)/home",
                                actionText: "View Active Session",
                                priority: "high",
                                issuer: "Security Office",
                                driverName: driverName,
                                vehicleBrand: vehicleBrand
                            );

                            notificationsSent++;
                            _logger.LogInformation("Sent {Hour}-hour in-app overstay reminder for vehicle {Plate} (Log {LogId})", hour, plateNumber, log.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send hourly in-app reminder for log {LogId}, hour {Hour}", log.Id, hour);
                        }
                    }
                }
            }
        }
    }

        return notificationsSent;
    }

    private async Task<bool> IsReferenceCodeProcessedAsync(Guid userAccountId, string referenceCode)
    {
        if (_sentReferenceCodes.ContainsKey(referenceCode))
        {
            return true;
        }

        var existsInDb = await _userNotificationRepository.ExistsByReferenceCodeAsync(userAccountId, referenceCode);
        if (existsInDb)
        {
            _sentReferenceCodes.TryAdd(referenceCode, 1);
            return true;
        }

        return false;
    }

    public static void ClearInMemoryCacheForTesting()
    {
        _sentReferenceCodes.Clear();
    }

    private static string BuildOverstayEmailHtml(
        string driverName,
        string plateNumber,
        string vehicleBrand,
        string vehicleType,
        string entryTime,
        string deadlineTime)
    {
        return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #1e293b 100%);border-top:4px solid #f59e0b;padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:12px;'>
              PARKFLOW PARKING ALERT
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Parking Overstay Notice</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Action Required • Campus Parking Time Exceeded</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;line-height:1.6;color:#1e293b;margin:0 0 16px;'>Hello <strong>{driverName}</strong>,</p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 24px;'>
              This is an official notice that your vehicle has <strong style='color:#dc2626;'>exceeded the authorized parking deadline</strong> on campus. Please proceed to your vehicle and exit the parking facility immediately to keep campus parking accessible.
            </p>

            <!-- Details Box -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Vehicle Plate Number</span><br>
                        <span style='font-size:16px;color:#0f172a;font-weight:800;font-family:monospace;letter-spacing:1px;'>{plateNumber}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Vehicle Description</span><br>
                        <span style='font-size:14px;color:#334155;font-weight:600;'>{vehicleBrand} ({vehicleType})</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Entry Time</span><br>
                        <span style='font-size:14px;color:#334155;font-weight:600;'>{entryTime}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Authorized Deadline</span><br>
                        <span style='font-size:15px;color:#dc2626;font-weight:700;'>{deadlineTime} (Exceeded)</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <div style='background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 18px;margin-bottom:24px;'>
              <p style='font-size:13px;line-height:1.5;color:#475569;margin:0;'>
                You will receive periodic in-app reminders while the vehicle remains parked.
              </p>
            </div>

            <div style='text-align:center;margin-top:24px;padding-top:18px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:11px;color:#94a3b8;margin:0;'>ParkFlow • Office of Security & Safety</p>
            </div>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background-color:#f8fafc;padding:18px 36px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:11px;color:#94a3b8;margin:0;'>© {DateTime.UtcNow.Year} ParkFlow System. All rights reserved.</p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
    }

    private static string BuildGracePeriodEmailHtml(
        string driverName,
        string plateNumber,
        string vehicleBrand,
        string vehicleType,
        string entryTime,
        string deadlineTime)
    {
        return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #1e3a8a 0%, #1e293b 100%);border-top:4px solid #f59e0b;padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:12px;'>
              PARKFLOW PARKING NOTICE
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Parking Grace Period Notice</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Action Required • 30-Minute Grace Period to Exit</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;line-height:1.6;color:#1e293b;margin:0 0 16px;'>Hello <strong>{driverName}</strong>,</p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 24px;'>
              This is a notification that your vehicle has entered its authorized <strong style='color:#d97706;'>30-minute grace period</strong>. Please proceed to your vehicle and exit the parking facility by <strong>{deadlineTime}</strong> to avoid parking overstay violations.
            </p>

            <!-- Details Box -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Vehicle Plate Number</span><br>
                        <span style='font-size:16px;color:#0f172a;font-weight:800;font-family:monospace;letter-spacing:1px;'>{plateNumber}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Vehicle Description</span><br>
                        <span style='font-size:14px;color:#334155;font-weight:600;'>{vehicleBrand} ({vehicleType})</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Entry Time</span><br>
                        <span style='font-size:14px;color:#334155;font-weight:600;'>{entryTime}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Grace Period Expiry / Exit Deadline</span><br>
                        <span style='font-size:15px;color:#d97706;font-weight:700;'>{deadlineTime}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <div style='background-color:#fffbeb;border:1px solid #fef3c7;border-radius:10px;padding:14px 18px;margin-bottom:24px;'>
              <p style='font-size:13px;line-height:1.5;color:#92400e;margin:0;'>
                <strong>Important:</strong> You have 30 minutes to safely exit the campus. Overstay alerts and violation logging begin immediately if your vehicle remains parked after <strong>{deadlineTime}</strong>.
              </p>
            </div>

            <div style='text-align:center;margin-top:24px;padding-top:18px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:11px;color:#94a3b8;margin:0;'>ParkFlow • Office of Security & Safety</p>
            </div>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background-color:#f8fafc;padding:18px 36px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:11px;color:#94a3b8;margin:0;'>© {DateTime.UtcNow.Year} ParkFlow System. All rights reserved.</p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
    }
}
