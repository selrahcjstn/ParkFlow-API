using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IParkingLogRoleService
{
    ParkingLogRoleDetails GetRoleDetails(UserProfile ownerProfile, Student? student, Personnel? personnel, Admin? admin);
}