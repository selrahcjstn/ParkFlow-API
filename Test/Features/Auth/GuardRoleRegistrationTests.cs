using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;
using ParkFlow.Application.Features.Auth.DTOs;

namespace Test.Features.Auth;

public class GuardRoleRegistrationTests
{
    [Fact]
    public async Task GuardRoleNeverBecomesAStudentEvenWhenStudentFieldsAreSent()
    {
        var students = new RegRoleStudentRepository(); var guards = new RegRoleGuardRepository();
        var handler = new RegisterManualAccountHandler(new FakeUserAccountRepository(), new RegRoleAuthIdentityRepository(), new RegRoleUserProfileRepository(),
            students, new RegRolePersonnelRepository(), guards, new FakePasswordHasher(), new FakeJwtService(), new RegisterManualAccountValidator());

        var result = await handler.Handle(new RegisterManualAccountCommand("aeron@example.test", "Password123!", "Aeron", "Santos", null, "09171234567",
            Role: "Guard", Student: new RegisterStudentDto("2023106763", "BSIT", "1A", 1), Guard: new RegisterGuardDto(2), IsAdminCreated: true), default);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(students.Students);
        Assert.Single(guards.Guards);
    }
}
