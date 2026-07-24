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
                        LastResetDate = _settings.LastResetDate
                    };
                }
            }
        }

        public static void Update(decimal rate, int gracePeriod, string academicYear, string semester)
        {
            lock (_lock)
            {
                _settings.ViolationRatePerHour = rate;
                _settings.GracePeriodMinutes = gracePeriod;
                if (!string.IsNullOrWhiteSpace(academicYear)) _settings.AcademicYear = academicYear;
                if (!string.IsNullOrWhiteSpace(semester)) _settings.CurrentSemester = semester;
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
