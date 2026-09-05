using System;

namespace ParkFlow.Application.Common
{
    public class SystemSettingsDto
    {
        public decimal ViolationRatePerHour { get; set; } = 100.00m;
        public int GracePeriodMinutes { get; set; } = 15;
        public string AcademicYear { get; set; } = "2026-2027";
        public string CurrentSemester { get; set; } = "1st Semester";
        public DateTime LastResetDate { get; set; } = DateTime.UtcNow;

        public int MaxParkingHours { get; set; } = 8;
        public int TotalCapacity { get; set; } = 500;
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
                        GracePeriodMinutes = _settings.GracePeriodMinutes,
                        AcademicYear = _settings.AcademicYear,
                        CurrentSemester = _settings.CurrentSemester,
                        LastResetDate = _settings.LastResetDate,
                        MaxParkingHours = _settings.MaxParkingHours,
                        TotalCapacity = _settings.TotalCapacity,
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
            bool autoApproveVerification = false)
        {
            lock (_lock)
            {
                _settings.ViolationRatePerHour = rate;
                _settings.GracePeriodMinutes = gracePeriod;
                if (!string.IsNullOrWhiteSpace(academicYear)) _settings.AcademicYear = academicYear;
                if (!string.IsNullOrWhiteSpace(semester)) _settings.CurrentSemester = semester;
                _settings.MaxParkingHours = maxParkingHours;
                _settings.TotalCapacity = totalCapacity;
                _settings.MaxVehiclesPerUser = maxVehiclesPerUser;
                _settings.MaintenanceMode = maintenanceMode;
                _settings.RfidInstantScanEnabled = rfidInstantScanEnabled;
                _settings.AutoApproveVerification = autoApproveVerification;
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
