namespace ParkFlow.Domain.Enums;

public enum EntryMethod
{
    QrCode,
    Manual,
    // Registered manual entry with normal schedule/reservation eligibility (no flat manual fee).
    ManualScheduled
}
