using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

public class PersonnelParkingPolicyTests
{
    private static DateTime Local(int day, int hour, int minute = 0) =>
        ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(new DateTime(2026, 10, day), new TimeSpan(hour, minute, 0));

    [Theory]
    [InlineData(4, 5, 0, 4, 21, 0, 0)] // Sunday, whole free window
    [InlineData(4, 20, 0, 4, 21, 1, 100)]
    [InlineData(4, 20, 0, 5, 6, 0, 900)] // no free reset at 5 AM
    [InlineData(4, 20, 0, 6, 6, 0, 3300)] // continuous across multiple days
    [InlineData(4, 4, 0, 4, 6, 0, 100)] // only the pre-opening hour is charged
    [InlineData(4, 4, 0, 4, 4, 30, 100)]
    [InlineData(4, 23, 0, 5, 6, 0, 700)] // never charges time before entry
    [InlineData(5, 6, 0, 5, 7, 0, 0)] // a new next-day session is free
    public void ChargesOnlyOutsideEntryDayWindow(int entryDay, int entryHour, int entryMinute,
        int exitDay, int exitHour, int exitMinute, int expected)
    {
        var settings = new SystemSettingsDto { ViolationRatePerHour = 100, FeeCalculationMode = "one_time" };
        var entry = Local(entryDay, entryHour, entryMinute);
        Assert.Equal(expected, PersonnelParkingPolicy.CalculateCharge(entry,
            Local(exitDay, exitHour, exitMinute), settings));
        Assert.Equal(Local(entryDay, 21), PersonnelParkingPolicy.GetDeadlineUtc(entry, settings));
    }

    [Fact]
    public void HonorsConfiguredHoursAndHourlyRateWithoutStudentGrace()
    {
        var settings = new SystemSettingsDto {
            PersonnelFreeParkingStart = "06:00", PersonnelFreeParkingEnd = "17:00",
            ViolationRatePerHour = 25, IsGracePeriodEnabled = true, GracePeriodMinutes = 15,
        };
        Assert.Equal(25m, PersonnelParkingPolicy.CalculateCharge(Local(4, 8), Local(4, 17, 1), settings));
        Assert.Equal(0m, PersonnelParkingPolicy.CalculateCharge(Local(4, 6), Local(4, 17), settings));
    }

    [Theory]
    [InlineData("21:00", "05:00")]
    [InlineData("05:00", "05:00")]
    [InlineData("24:00", "21:00")]
    [InlineData("bad", "21:00")]
    public void RejectsInvalidWindows(string start, string end) =>
        Assert.False(PersonnelParkingPolicy.TryGetHours(new SystemSettingsDto {
            PersonnelFreeParkingStart = start, PersonnelFreeParkingEnd = end,
        }, out _, out _));

    [Theory]
    [InlineData(Roles.UniversityStaff, true)]
    [InlineData(Roles.NonAcademicPersonnel, true)]
    [InlineData(Roles.Student, false)]
    [InlineData(Roles.Admin, false)]
    public void OnlyFacultyAndStaffQualify(Roles role, bool expected) =>
        Assert.Equal(expected, PersonnelParkingPolicy.AppliesTo(new Personnel(Guid.NewGuid(), "ID-1", "Office", role)));

    [Fact]
    public void VisitorDoesNotQualify() => Assert.False(PersonnelParkingPolicy.AppliesTo(null));

    [Theory]
    [InlineData(9, 0)] // 9:44 AM is within the configured free window.
    [InlineData(21, 100)] // 9:44 PM entry is allowed, but parking time is chargeable.
    public void NineFortyFourEntryUsesTheConfiguredFreeWindow(int hour, decimal expectedCharge)
    {
        var settings = new SystemSettingsDto { ViolationRatePerHour = 100 };
        var entry = Local(8, hour, 44);
        Assert.Equal(expectedCharge, PersonnelParkingPolicy.CalculateCharge(entry, entry.AddMinutes(30), settings));
    }
}
