using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

// Financial amounts use the shared operational deadline; they never change entry/exit timing.
public static class ParkingCharges
{
    public static bool HasConfiguredFees(VehicleType type)
    {
        var r=SystemSettingsStore.Current.ForVehicle(type);
        return new[] {r.EntryFee,r.StudentEntryFee,r.FacultyEntryFee,r.PersonnelEntryFee,r.VisitorFee,r.VipEntryFee,r.ViolationRatePerHour,r.BaseFee,r.DailyOverstayFee}.Any(x=>x>0m);
    }
    public static Violation? Checkout(ParkingLog log, Vehicle vehicle, Personnel? personnel,
        bool student, ParkingReservation? reservation, double overstayHours, DateTime exitUtc)
    {
        var settings = SystemSettingsStore.Current;
        var rate = settings.ForVehicle(vehicle.VehicleType);
        var days = Math.Max(0, (ParkingTimeHelper.ConvertUtcToPhilippinesTime(exitUtc).Date -
            ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime).Date).Days);
        decimal amount;
        if (reservation?.Type == ReservationType.Special) amount = rate.VipEntryFee;
        else if (PersonnelParkingPolicy.IsFaculty(personnel)) amount = rate.FacultyEntryFee;
        else if (PersonnelParkingPolicy.AppliesTo(personnel))
            amount = rate.PersonnelEntryFee + (decimal)Math.Ceiling(Math.Max(0, overstayHours)) * rate.ViolationRatePerHour;
        else if (log.EntryMethod == EntryMethod.Manual) amount = rate.EntryFee + days * rate.DailyOverstayFee;
        else if (reservation != null) amount = rate.EntryFee + (overstayHours > 0 ? (days + 1) * rate.DailyOverstayFee : 0);
        else amount = (student ? rate.StudentEntryFee : rate.EntryFee) +
            new ViolationService().CalculatePenaltyForVehicle(TimeSpan.FromHours(Math.Max(0, overstayHours)), vehicle.VehicleType);
        if (amount <= 0m) return null; // A zero total creates no outstanding obligation.
        return new Violation(log.Id, amount, overstayHours > 0 ? ViolationType.Overstay :
            reservation != null ? ViolationType.ReservationCharge : ViolationType.ManualParkingCharge);
    }
}
