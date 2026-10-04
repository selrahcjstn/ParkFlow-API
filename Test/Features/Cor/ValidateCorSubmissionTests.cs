using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Vehicles;

namespace Test.Features.Cor;

public class FakeCorSubmissionRepository : ICorSubmissionRepository
{
    public List<CorSubmission> Submissions { get; } = new();

    public Task AddCorSubmissionAsync(CorSubmission corSubmission)
    {
        Submissions.Add(corSubmission);
        return Task.CompletedTask;
    }

    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id)
    {
        return Task.FromResult(Submissions.FirstOrDefault(s => s.Id == id));
    }

    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userAccountId, string academicTerm)
    {
        return Task.FromResult(Submissions.FirstOrDefault(s => s.UserAccountId == userAccountId && s.AcademicTerm == academicTerm));
    }

    public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userId)
    {
        return Task.FromResult(Submissions.LastOrDefault(s => s.UserAccountId == userId));
    }

    public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync()
    {
        return Task.FromResult<IEnumerable<CorSubmission>>(Submissions.ToList());
    }

    public Task UpdateCorSubmissionAsync(CorSubmission corSubmission)
    {
        var existing = Submissions.FirstOrDefault(s => s.Id == corSubmission.Id);
        if (existing != null)
        {
            Submissions.Remove(existing);
            Submissions.Add(corSubmission);
        }
        return Task.CompletedTask;
    }

    public Task DeleteCorSubmissionAsync(CorSubmission corSubmission)
    {
        Submissions.Remove(corSubmission);
        return Task.CompletedTask;
    }
}

public class FakeUserAccountRepository : IUserAccountRepository
{
    public List<UserAccount> Users { get; } = new();

    public Task<UserAccount?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    }

    public Task<UserAccount?> GetByEmailAsync(string email)
    {
        return Task.FromResult(Users.FirstOrDefault(u => u.PrimaryEmail == email));
    }

    public Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider authProvider, string externalProviderId)
    {
        return Task.FromResult<UserAccount?>(null);
    }

    public Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber)
    {
        return Task.FromResult(Users.FirstOrDefault(u => u.PhoneNumber == phoneNumber));
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        return Task.FromResult(Users.Any(u => u.PrimaryEmail == email && (excludeUserId == null || u.Id != excludeUserId)));
    }

    public Task<IEnumerable<UserAccount>> ListAllAsync()
    {
        return Task.FromResult<IEnumerable<UserAccount>>(Users.ToList());
    }

    public Task AddAsync(UserAccount user)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserAccount user)
    {
        var existing = Users.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            Users.Remove(existing);
            Users.Add(user);
        }
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var existing = Users.FirstOrDefault(u => u.Id == id);
        if (existing != null)
        {
            Users.Remove(existing);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

public class ValidateCorSubmissionTests
{
    private readonly FakeCorSubmissionRepository _corRepo = new();
    private readonly FakeUserAccountRepository _userRepo = new();
    private readonly InMemoryVehicleRepository _vehicleRepo = new();
    private readonly ValidateCorSubmissionValidator _validator = new();

    [Fact]
    public async Task ValidateCorSubmission_WhenRejected_ShouldNotDeleteUserVehicles()
    {
        // Arrange
        var user = new UserAccount("hashed_password", "09123456789");
        await _userRepo.AddAsync(user);

        var cor = new CorSubmission(user.Id, "1st Term", "https://example.com/cor.pdf", verificationStatus: CorVerificationStatus.Pending);
        await _corRepo.AddCorSubmissionAsync(cor);

        var vehicle1 = new Vehicle(user.Id, "ABC-111", "Toyota", "hash1", VehicleType.Car);
        var vehicle2 = new Vehicle(user.Id, "XYZ-222", "Honda", "hash2", VehicleType.Motorcycle);
        await _vehicleRepo.AddAsync(vehicle1);
        await _vehicleRepo.AddAsync(vehicle2);

        var handler = new ValidateCorSubmissionHandler(
            _corRepo,
            _userRepo,
            _vehicleRepo,
            _validator
        );

        var command = new ValidateCorSubmissionCommand(cor.Id, CorVerificationStatus.Rejected, "Invalid study load document.");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedUser = await _userRepo.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(AccountStatus.PendingVerification, updatedUser.Status);

        var vehicles = (await _vehicleRepo.GetByOwnerIdAsync(user.Id)).ToList();
        Assert.Equal(2, vehicles.Count);
        Assert.Contains(vehicles, v => v.PlateNumber == "ABC-111");
        Assert.Contains(vehicles, v => v.PlateNumber == "XYZ-222");
    }
}
