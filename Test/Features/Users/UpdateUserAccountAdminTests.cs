using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.UpdateUserAccountAdmin;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Test.Features.Users;

public class FakeUserProfileRepository : IUserProfileRepository
{
    public List<UserProfile> Profiles { get; } = new();

    public Task AddAsync(UserProfile profile)
    {
        Profiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task<UserProfile?> GetByIdAsync(Guid id) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.Id == id));

    public Task<UserProfile?> GetByUserIdAsync(Guid userId) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.UserAccountId == userId));

    public Task UpdateAsync(UserProfile profile)
    {
        var existing = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (existing != null)
        {
            Profiles.Remove(existing);
            Profiles.Add(profile);
        }
        return Task.CompletedTask;
    }
}

public class FakeStudentRepository : IStudentRepository
{
    public List<Student> Students { get; } = new();

    public Task AddAsync(Student student)
    {
        Students.Add(student);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Student student)
    {
        Students.Remove(student);
        return Task.CompletedTask;
    }

    public Task<Student?> GetByStudentNumberAsync(string studentNumber, Guid? excludeProfileId = null) =>
        Task.FromResult(Students.FirstOrDefault(s => s.UserProfileId != excludeProfileId && Student.NormalizeNumber(s.StudentNumber) == Student.NormalizeNumber(studentNumber)));

    public Task<Student?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(Students.FirstOrDefault(s => s.UserProfileId == userProfileId));

    public Task UpdateAsync(Student student)
    {
        var existing = Students.FirstOrDefault(s => s.UserProfileId == student.UserProfileId);
        if (existing != null)
        {
            Students.Remove(existing);
            Students.Add(student);
        }
        return Task.CompletedTask;
    }
}

public class FakePersonnelRepository : IPersonnelRepository
{
    public List<Personnel> PersonnelList { get; } = new();

    public Task AddAsync(Personnel personnel)
    {
        PersonnelList.Add(personnel);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Personnel personnel)
    {
        PersonnelList.Remove(personnel);
        return Task.CompletedTask;
    }

    public Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber, Guid? excludeProfileId = null) =>
        Task.FromResult(PersonnelList.FirstOrDefault(p => p.UserProfileId != excludeProfileId && Personnel.NormalizeId(p.IdCardNumber) == Personnel.NormalizeId(idCardNumber)));

    public Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(PersonnelList.FirstOrDefault(p => p.UserProfileId == userProfileId));

    public Task UpdateAsync(Personnel personnel)
    {
        var existing = PersonnelList.FirstOrDefault(p => p.UserProfileId == personnel.UserProfileId);
        if (existing != null)
        {
            PersonnelList.Remove(existing);
            PersonnelList.Add(personnel);
        }
        return Task.CompletedTask;
    }
}

public class FakeGuardRepository : IGuardRepository
{
    public List<Guard> Guards { get; } = new();

    public Task AddAsync(Guard guard)
    {
        Guards.Add(guard);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guard guard)
    {
        Guards.Remove(guard);
        return Task.CompletedTask;
    }

    public Task<Guard?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(Guards.FirstOrDefault(g => g.UserProfileId == userProfileId));

    public Task UpdateAsync(Guard guard)
    {
        var existing = Guards.FirstOrDefault(g => g.UserProfileId == guard.UserProfileId);
        if (existing != null)
        {
            Guards.Remove(existing);
            Guards.Add(guard);
        }
        return Task.CompletedTask;
    }
}

public class FakeAdminRepository : IAdminRepository
{
    public List<Admin> Admins { get; } = new();

    public Task AddAsync(Admin admin)
    {
        Admins.Add(admin);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Admin admin)
    {
        Admins.Remove(admin);
        return Task.CompletedTask;
    }

    public Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(Admins.FirstOrDefault(a => a.UserProfileId == userProfileId));

    public Task<IEnumerable<Admin>> ListAllAsync() =>
        Task.FromResult<IEnumerable<Admin>>(Admins);
}

public class UpdateUserAccountAdminTests
{
    private readonly FakeUserAccountRepository _userAccountRepo;
    private readonly FakeUserProfileRepository _userProfileRepo;
    private readonly FakeStudentRepository _studentRepo;
    private readonly FakePersonnelRepository _personnelRepo;
    private readonly FakeGuardRepository _guardRepo;
    private readonly FakeAdminRepository _adminRepo;
    private readonly FakePasswordHasher _passwordHasher;
    private readonly UpdateUserAccountAdminHandler _handler;

    public UpdateUserAccountAdminTests()
    {
        _userAccountRepo = new FakeUserAccountRepository();
        _userProfileRepo = new FakeUserProfileRepository();
        _studentRepo = new FakeStudentRepository();
        _personnelRepo = new FakePersonnelRepository();
        _guardRepo = new FakeGuardRepository();
        _adminRepo = new FakeAdminRepository();
        _passwordHasher = new FakePasswordHasher();

        _handler = new UpdateUserAccountAdminHandler(
            _userAccountRepo,
            _userProfileRepo,
            _studentRepo,
            _personnelRepo,
            _guardRepo,
            _adminRepo,
            _passwordHasher
        );
    }

    [Fact]
    public async Task Handle_ShouldSuccessfullyUpdateUserProfile_AndPhotoUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount("hash", "+639123456789");
        var idProp = typeof(BaseEntity).GetProperty("Id");
        idProp?.SetValue(user, userId);

        var profile = new UserProfile(userId, "Juan", "Dela Cruz", null, "https://old.url/avatar.jpg");
        user.UserProfile = profile;
        _userAccountRepo.Users.Add(user);
        _userProfileRepo.Profiles.Add(profile);

        var requestDto = new UpdateUserAccountAdminRequest(
            FirstName: "Juanito",
            LastName: "Dela Cruz",
            MiddleName: "Santos",
            Email: "juanito@example.com",
            PhoneNumber: "+639987654321",
            Role: "Student",
            Status: "Active",
            PhotoUrl: "https://res.cloudinary.com/parkflow/image/upload/v1234/new_photo.jpg",
            Password: null,
            Student: new UpdateUserStudentRequest("2024-00123", "BSIT", "A", 2),
            Personnel: null,
            Guard: null
        );

        var command = new UpdateUserAccountAdminCommand(userId, requestDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Data);
        Assert.Equal("Juanito", profile.FirstName);
        Assert.Equal("Santos", profile.MiddleName);
        Assert.Equal("https://res.cloudinary.com/parkflow/image/upload/v1234/new_photo.jpg", profile.ProfilePictureUrl);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreOversizedBase64PhotoUrl_AndNotCrash()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount("hash", "+639123456789");
        var idProp = typeof(BaseEntity).GetProperty("Id");
        idProp?.SetValue(user, userId);

        var profile = new UserProfile(userId, "Maria", "Clara", null, "https://old.url/avatar.jpg");
        user.UserProfile = profile;
        _userAccountRepo.Users.Add(user);
        _userProfileRepo.Profiles.Add(profile);

        // 3000-character oversized data URL
        var oversizedDataUrl = "data:image/jpeg;base64," + new string('A', 3000);

        var requestDto = new UpdateUserAccountAdminRequest(
            FirstName: "Maria",
            LastName: "Clara",
            MiddleName: null,
            Email: "maria@example.com",
            PhoneNumber: "+639123456789",
            Role: "Student",
            Status: "Active",
            PhotoUrl: oversizedDataUrl,
            Password: null,
            Student: new UpdateUserStudentRequest("2024-00124", "BSCS", "B", 1),
            Personnel: null,
            Guard: null
        );

        var command = new UpdateUserAccountAdminCommand(userId, requestDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("https://old.url/avatar.jpg", profile.ProfilePictureUrl);
    }
}
