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

    // The single place a parking charge is priced. Entry fee (usually 0) depends on who parked;
    // the overstay penalty always follows the Super Admin "Overstay fee mode" for every driver type.
    public static decimal Amount(VehicleType vehicleType, EntryMethod method, Personnel? personnel, bool student,
        ParkingReservation? reservation, double overstayHours)
    {
        var rate = SystemSettingsStore.Current.ForVehicle(vehicleType);
        if (reservation?.Type == ReservationType.Special) return rate.VipEntryFee;
        var entryFee =
            PersonnelParkingPolicy.IsFaculty(personnel) ? rate.FacultyEntryFee :
            PersonnelParkingPolicy.AppliesTo(personnel) ? rate.PersonnelEntryFee :
            method == EntryMethod.Manual || reservation != null ? rate.EntryFee :
            student ? rate.StudentEntryFee : rate.EntryFee;
        var penalty = new ViolationService().CalculatePenaltyForVehicle(
            TimeSpan.FromHours(Math.Max(0, overstayHours)), vehicleType);
        return entryFee + penalty;
    }

    public static Violation? Checkout(ParkingLog log, Vehicle vehicle, Personnel? personnel,
        bool student, ParkingReservation? reservation, double overstayHours, DateTime exitUtc)
    {
        var amount = Amount(vehicle.VehicleType, log.EntryMethod, personnel, student, reservation, overstayHours);
        if (amount <= 0m) return null; // A zero total creates no outstanding obligation.
        return new Violation(log.Id, amount, overstayHours > 0 ? ViolationType.Overstay :
            reservation != null ? ViolationType.ReservationCharge : ViolationType.ManualParkingCharge);
    }
}
