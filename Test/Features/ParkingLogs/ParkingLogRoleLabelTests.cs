using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

public class ParkingLogRoleLabelTests
{
    [Theory]
    [InlineData(Roles.UniversityStaff, "Faculty", 2)]
    [InlineData(Roles.NonAcademicPersonnel, "University Staff", 3)]
    public void GateSessionAndHistoryResponsesDistinguishFacultyFromUniversityStaff(Roles role, string expected, int persistedId)
    {
        var profile = new UserProfile(Guid.NewGuid(), "Employee", "One", null, null);
        var employee = new Personnel(profile.Id, "EMP-1", "Office", role);
        var details = new ParkingLogRoleService().GetRoleDetails(profile, null, employee, null);
        Assert.Equal(expected, details.Role);
        Assert.Equal("EMP-1", details.IdNumber);
        Assert.Equal(persistedId, (int)role);
    }

    [Fact]
    public void StudentDisplayLabelAndIdentifierRemainClear()
    {
        var profile = new UserProfile(Guid.NewGuid(), "Student", "One", null, null);
        var student = new Student(profile.Id, "202600123", "BSIT", "1A", 1);
        var details = new ParkingLogRoleService().GetRoleDetails(profile, student, null, null);
        Assert.Equal("Student", details.Role);
        Assert.Equal(student.StudentNumber, details.IdNumber);
        Assert.Equal(1, (int)Roles.Student);
    }
}
