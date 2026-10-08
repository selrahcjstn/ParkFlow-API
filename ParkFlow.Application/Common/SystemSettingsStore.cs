using System;

namespace ParkFlow.Application.Common
{
    public class SystemSettingsDto
    {
        public decimal ViolationRatePerHour { get; set; } = 100.00m;
        public string FeeCalculationMode { get; set; } = "per_hour"; // "per_hour", "per_day", "one_time", "one_time_hourly", "no_fee"
        public decimal BaseFee { get; set; } = 50.00m;
        public string PersonnelFreeParkingStart { get; set; } = "05:00";
        public string PersonnelFreeParkingEnd { get; set; } = "21:00";

        public bool IsGracePeriodEnabled { get; set; } = true;
        public int GracePeriodMinutes { get; set; } = 15;

        public bool IsEarlyParkingAllowed { get; set; } = true;
        public int EarlyParkingMinutes { get; set; } = 15;

        public string AcademicYear { get; set; } = "2026-2027";
        public string CurrentSemester { get; set; } = "1st Semester";
        public DateTime LastResetDate { get; set; } = DateTime.UtcNow;

        public int MaxParkingHours { get; set; } = 8;
        public int TotalCapacity { get; set; } = 500;
        public int ReservationAllocationPercent { get; set; } = 30;
        public int MaxVehiclesPerUser { get; set; } = 5;
        public bool MaintenanceMode { get; set; } = false;
        public bool RfidInstantScanEnabled { get; set; } = true;
        public bool AutoApproveVerification { get; set; } = false;
    }

    public static class SystemSettingsStore
    {
        private static readonly object _lock = new();
        private static SystemSettingsDto _settings = new();

        public static SystemSettingsDto Current
        {
            get
            {
                lock (_lock)
                {
                    return new SystemSettingsDto
                    {
                        ViolationRatePerHour = _settings.ViolationRatePerHour,
                        FeeCalculationMode = _settings.FeeCalculationMode ?? "per_hour",
                        BaseFee = _settings.BaseFee,
                        PersonnelFreeParkingStart = _settings.PersonnelFreeParkingStart,
                        PersonnelFreeParkingEnd = _settings.PersonnelFreeParkingEnd,
                        IsGracePeriodEnabled = _settings.IsGracePeriodEnabled,
                        GracePeriodMinutes = _settings.GracePeriodMinutes,
                        IsEarlyParkingAllowed = _settings.IsEarlyParkingAllowed,
                        EarlyParkingMinutes = _settings.EarlyParkingMinutes,
                        AcademicYear = _settings.AcademicYear,
                        CurrentSemester = _settings.CurrentSemester,
                        LastResetDate = _settings.LastResetDate,
                        MaxParkingHours = _settings.MaxParkingHours,
                        TotalCapacity = _settings.TotalCapacity,
                        ReservationAllocationPercent = _settings.ReservationAllocationPercent,
                        MaxVehiclesPerUser = _settings.MaxVehiclesPerUser,
                        MaintenanceMode = _settings.MaintenanceMode,
                        RfidInstantScanEnabled = _settings.RfidInstantScanEnabled,
                        AutoApproveVerification = _settings.AutoApproveVerification
                    };
                }
            }
        }

        public static void Update(
            decimal rate,
            int gracePeriod,
            string academicYear,
            string semester,
            int maxParkingHours = 8,
            int totalCapacity = 500,
            int maxVehiclesPerUser = 5,
            bool maintenanceMode = false,
            bool rfidInstantScanEnabled = true,
            bool autoApproveVerification = false,
            string? feeCalculationMode = null,
            decimal? baseFee = null,
            bool? isGracePeriodEnabled = null,
            bool? isEarlyParkingAllowed = null,
            int? earlyParkingMinutes = null,
            string? personnelFreeParkingStart = null,
            string? personnelFreeParkingEnd = null,
            int? reservationAllocationPercent = null)
        {
            lock (_lock)
            {
                _settings.ViolationRatePerHour = rate;
                _settings.GracePeriodMinutes = gracePeriod;
                if (!string.IsNullOrWhiteSpace(academicYear)) _settings.AcademicYear = academicYear;
                if (!string.IsNullOrWhiteSpace(semester)) _settings.CurrentSemester = semester;
                _settings.MaxParkingHours = maxParkingHours;
                _settings.TotalCapacity = totalCapacity;
                if (reservationAllocationPercent.HasValue) _settings.ReservationAllocationPercent = reservationAllocationPercent.Value;
                _settings.MaxVehiclesPerUser = maxVehiclesPerUser;
                _settings.MaintenanceMode = maintenanceMode;
                _settings.RfidInstantScanEnabled = rfidInstantScanEnabled;
                _settings.AutoApproveVerification = autoApproveVerification;

                if (!string.IsNullOrWhiteSpace(feeCalculationMode)) _settings.FeeCalculationMode = feeCalculationMode;
                if (baseFee.HasValue) _settings.BaseFee = baseFee.Value;
                if (isGracePeriodEnabled.HasValue) _settings.IsGracePeriodEnabled = isGracePeriodEnabled.Value;
                if (isEarlyParkingAllowed.HasValue) _settings.IsEarlyParkingAllowed = isEarlyParkingAllowed.Value;
                if (earlyParkingMinutes.HasValue) _settings.EarlyParkingMinutes = earlyParkingMinutes.Value;
                if (personnelFreeParkingStart != null) _settings.PersonnelFreeParkingStart = personnelFreeParkingStart;
                if (personnelFreeParkingEnd != null) _settings.PersonnelFreeParkingEnd = personnelFreeParkingEnd;
            }
        }

        public static void RecordReset()
        {
            lock (_lock)
            {
                _settings.LastResetDate = DateTime.UtcNow;
            }
        }
    }
}
