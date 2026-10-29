using System;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Common
{
    public class VehicleParkingSettingsDto
    {
        public int TotalCapacity { get; set; } = 250;
        public int ReservationAllocationSlots { get; set; } = 75;
        public decimal ViolationRatePerHour { get; set; }
        public decimal BaseFee { get; set; }
        public decimal EntryFee { get; set; }
        public decimal DailyOverstayFee { get; set; }
        public decimal StudentEntryFee { get; set; }
        public decimal FacultyEntryFee { get; set; }
        public decimal PersonnelEntryFee { get; set; }
        public decimal VisitorFee { get; set; }
        public decimal VipEntryFee { get; set; }
        public VehicleParkingSettingsDto Copy() => (VehicleParkingSettingsDto)MemberwiseClone();
    }

    public class SystemSettingsDto
    {
        public decimal ViolationRatePerHour { get; set; }
        public decimal BaseFee { get; set; }
        public string FeeCalculationMode { get; set; } = "per_hour";
        public string PersonnelFreeParkingStart { get; set; } = "05:00";
        public string PersonnelFreeParkingEnd { get; set; } = "21:00";

        public bool IsGracePeriodEnabled { get; set; } = true;
        public int GracePeriodMinutes { get; set; } = 15;


        public string AcademicYear { get; set; } = "2026-2027";
        public string CurrentSemester { get; set; } = "1st Semester";
        public DateTime LastResetDate { get; set; } = DateTime.UtcNow;

        public VehicleParkingSettingsDto CarParking { get; set; } = new();
        public VehicleParkingSettingsDto MotorcycleParking { get; set; } = new();
        // Legacy electric bikes use motorcycle spaces.
        public VehicleParkingSettingsDto ForVehicle(VehicleType type) => type == VehicleType.Car ? CarParking : MotorcycleParking;

        public int MaxParkingHours { get; set; } = 8;
        public int TotalCapacity { get; set; } = 500;
        public int ReservationAllocationSlots { get; set; } = 150;
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
                        BaseFee = _settings.BaseFee,
                        FeeCalculationMode = _settings.FeeCalculationMode,
                        PersonnelFreeParkingStart = _settings.PersonnelFreeParkingStart,
                        PersonnelFreeParkingEnd = _settings.PersonnelFreeParkingEnd,
                        IsGracePeriodEnabled = _settings.IsGracePeriodEnabled,
                        GracePeriodMinutes = _settings.GracePeriodMinutes,
                        AcademicYear = _settings.AcademicYear,
                        CurrentSemester = _settings.CurrentSemester,
                        LastResetDate = _settings.LastResetDate,
                        MaxParkingHours = _settings.MaxParkingHours,
                        CarParking = _settings.CarParking.Copy(),
                        MotorcycleParking = _settings.MotorcycleParking.Copy(),
                        TotalCapacity = _settings.CarParking.TotalCapacity + _settings.MotorcycleParking.TotalCapacity,
                        ReservationAllocationSlots = _settings.CarParking.ReservationAllocationSlots + _settings.MotorcycleParking.ReservationAllocationSlots,
                        MaxVehiclesPerUser = _settings.MaxVehiclesPerUser,
                        MaintenanceMode = _settings.MaintenanceMode,
                        RfidInstantScanEnabled = _settings.RfidInstantScanEnabled,
                        AutoApproveVerification = _settings.AutoApproveVerification
                    };
                }
            }
        }

        public static void Update(
            int gracePeriod,
            string academicYear,
            string semester,
            int maxParkingHours = 8,
            int totalCapacity = 500,
            int maxVehiclesPerUser = 5,
            bool maintenanceMode = false,
            bool rfidInstantScanEnabled = true,
            bool autoApproveVerification = false,
            bool? isGracePeriodEnabled = null,
            string? personnelFreeParkingStart = null,
            string? personnelFreeParkingEnd = null,
            int? reservationAllocationSlots = null,
            VehicleParkingSettingsDto? carParking = null,
            VehicleParkingSettingsDto? motorcycleParking = null)
        {
            lock (_lock)
            {
                _settings.GracePeriodMinutes = gracePeriod;
                if (!string.IsNullOrWhiteSpace(academicYear)) _settings.AcademicYear = academicYear;
                if (!string.IsNullOrWhiteSpace(semester)) _settings.CurrentSemester = semester;
                _settings.MaxParkingHours = maxParkingHours;
                _settings.TotalCapacity = totalCapacity;
                if (reservationAllocationSlots.HasValue) _settings.ReservationAllocationSlots = reservationAllocationSlots.Value;
                if (carParking != null) _settings.CarParking = carParking.Copy();
                if (motorcycleParking != null) _settings.MotorcycleParking = motorcycleParking.Copy();
                if (carParking == null && motorcycleParking == null)
                {
                    // Keep older internal callers compatible with aggregate settings.
                    _settings.CarParking.TotalCapacity = totalCapacity / 2;
                    _settings.MotorcycleParking.TotalCapacity = totalCapacity - totalCapacity / 2;
                    if (reservationAllocationSlots.HasValue)
                    {
                        _settings.CarParking.ReservationAllocationSlots = reservationAllocationSlots.Value / 2;
                        _settings.MotorcycleParking.ReservationAllocationSlots = reservationAllocationSlots.Value - reservationAllocationSlots.Value / 2;
                    }
                }
                _settings.MaxVehiclesPerUser = maxVehiclesPerUser;
                _settings.MaintenanceMode = maintenanceMode;
                _settings.RfidInstantScanEnabled = rfidInstantScanEnabled;
                _settings.AutoApproveVerification = autoApproveVerification;

                if (isGracePeriodEnabled.HasValue) _settings.IsGracePeriodEnabled = isGracePeriodEnabled.Value;
                if (personnelFreeParkingStart != null) _settings.PersonnelFreeParkingStart = personnelFreeParkingStart;
                if (personnelFreeParkingEnd != null) _settings.PersonnelFreeParkingEnd = personnelFreeParkingEnd;
            }
        }

        public static void Replace(SystemSettingsDto settings)
        {
            lock (_lock)
            {
                _settings = System.Text.Json.JsonSerializer.Deserialize<SystemSettingsDto>(
                    System.Text.Json.JsonSerializer.Serialize(settings))!;
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
