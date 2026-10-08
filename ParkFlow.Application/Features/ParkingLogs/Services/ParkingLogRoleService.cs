using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ParkingLogRoleService : IParkingLogRoleService
{
    public ParkingLogRoleDetails GetRoleDetails(UserProfile ownerProfile, Student? student, Personnel? personnel, Admin? admin)
    {
        if (student != null)
        {
            return new ParkingLogRoleDetails(
                "Student",
                student.StudentNumber,
                student.Course,
                student.YearLevel,
                student.Section,
                null);
        }

        if (personnel != null)
        {
            return new ParkingLogRoleDetails(
                personnel.Role == Roles.NonAcademicPersonnel ? "University Staff" : "Faculty",
                personnel.IdCardNumber,
                null,
                null,
                null,
                personnel.Department);
        }

        if (admin != null)
        {
            return new ParkingLogRoleDetails("admin", string.Empty, null, null, null, null);
        }

        if (ownerProfile.Guard != null)
        {
            return new ParkingLogRoleDetails("guard", string.Empty, null, null, null, null);
        }

        return new ParkingLogRoleDetails("unknown", string.Empty, null, null, null, null);
    }
}
