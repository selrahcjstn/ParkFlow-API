using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.CreateUserAccount;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.Users.Commands.CreateUserAccount;

public class CreateUserAccountHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessAndPersistsUser()
    {
        var repo = new InMemoryUserAccountRepository();
        var validator = new CreateUserAccountValidator();
        var passwordHasher = new FakePasswordHasher();
        var handler = new CreateUserAccountHandler(repo, validator, passwordHasher);

        var command = new CreateUserAccountCommand(
            Email: "test@example.com",
            Password: "Password1!",
            PhoneNumber: "09123456789",
            Role: Roles.Student);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorCode.None, result.ErrorCode);
        Assert.NotEqual(Guid.Empty, result.Data);

        Assert.Single(repo.Users);
        var savedUser = repo.Users[0];
        Assert.Equal("test@example.com", savedUser.Email);
        Assert.Equal("HASHED:Password1!", savedUser.PasswordHash);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsConflict()
    {
        var repo = new InMemoryUserAccountRepository();
        repo.Seed(new UserAccount(
            email: "dupe@example.com",
            passwordHash: "HASHED:Password1!",
            phoneNumber: "09123456789",
            role: Roles.Student));

        var validator = new CreateUserAccountValidator();
        var passwordHasher = new FakePasswordHasher();
        var handler = new CreateUserAccountHandler(repo, validator, passwordHasher);

        var command = new CreateUserAccountCommand(
            Email: "dupe@example.com",
            Password: "Password1!",
            PhoneNumber: "09123456789",
            Role: Roles.Student);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsBadRequest()
    {
        var repo = new InMemoryUserAccountRepository();
        var validator = new CreateUserAccountValidator();
        var passwordHasher = new FakePasswordHasher();
        var handler = new CreateUserAccountHandler(repo, validator, passwordHasher);

        var command = new CreateUserAccountCommand(
            Email: "not-an-email",
            Password: "Password1!",
            PhoneNumber: "09123456789",
            Role: Roles.Student);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("Email must be a valid email address", result.Message);
    }

    [Fact]
    public async Task Handle_WithPasswordMissingUppercase_ReturnsBadRequest()
    {
        var repo = new InMemoryUserAccountRepository();
        var validator = new CreateUserAccountValidator();
        var passwordHasher = new FakePasswordHasher();
        var handler = new CreateUserAccountHandler(repo, validator, passwordHasher);

        var command = new CreateUserAccountCommand(
            Email: "test@example.com",
            Password: "password1!",
            PhoneNumber: "09123456789",
            Role: Roles.Student);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("uppercase", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithInvalidPhoneNumber_ReturnsBadRequest()
    {
        var repo = new InMemoryUserAccountRepository();
        var validator = new CreateUserAccountValidator();
        var passwordHasher = new FakePasswordHasher();
        var handler = new CreateUserAccountHandler(repo, validator, passwordHasher);

        var command = new CreateUserAccountCommand(
            Email: "test@example.com",
            Password: "Password1!",
            PhoneNumber: "123",
            Role: Roles.Student);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("Phone number", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"HASHED:{password}";

        public bool VerifyPassword(string hashedPassword, string providedPassword)
            => hashedPassword == $"HASHED:{providedPassword}";
    }

    private sealed class InMemoryUserAccountRepository : IUserAccountRepository
    {
        private readonly List<UserAccount> _users = new();

        public IReadOnlyList<UserAccount> Users => _users;

        public Task<UserAccount?> GetByEmailAsync(string email)
            => Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

        public Task<UserAccount?> GetByIdAsync(Guid id)
            => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task AddAsync(UserAccount user)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserAccount user)
        {
            var index = _users.FindIndex(u => u.Id == user.Id);
            if (index >= 0)
                _users[index] = user;
            else
                _users.Add(user);

            return Task.CompletedTask;
        }

        public void Seed(UserAccount user) => _users.Add(user);
    }
}
