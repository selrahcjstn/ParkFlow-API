using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;
using ParkFlow.Application.Features.Files.Commands.UploadCorDocument;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Test.Features.Files;

public class FakeCloudinaryService : ICloudinaryService
{
    public bool ThrowException { get; set; }
    public string ExpectedUrl { get; set; } = "https://res.cloudinary.com/test/image/upload/v1234/parkflow/profiles/test.png";
    public string ExpectedPdfUrl { get; set; } = "https://res.cloudinary.com/test/raw/upload/v1234/parkflow/cor/test.pdf";
    public string ExpectedPublicId { get; set; } = "parkflow/profiles/test";
    public string ExpectedPdfPublicId { get; set; } = "parkflow/cor/test";

    public List<string> DeletedPublicIds { get; } = new();

    public Task<(string SecureUrl, string PublicId)> UploadImageAsync(IFormFile file, string folder)
    {
        if (ThrowException)
            throw new Exception("Cloudinary connection lost.");

        return Task.FromResult((ExpectedUrl, ExpectedPublicId));
    }

    public Task<(string SecureUrl, string PublicId)> UploadPdfAsync(IFormFile file, string folder)
    {
        if (ThrowException)
            throw new Exception("Cloudinary raw upload failed.");

        return Task.FromResult((ExpectedPdfUrl, ExpectedPdfPublicId));
    }

    public Task DeleteFileAsync(string publicId, bool isImage = true)
    {
        if (ThrowException)
            throw new Exception("Cloudinary delete failed.");

        DeletedPublicIds.Add(publicId);
        return Task.CompletedTask;
    }
}

public class InMemoryUserProfileRepository : IUserProfileRepository
{
    public List<UserProfile> Profiles { get; } = new();

    public Task<UserProfile?> GetByIdAsync(Guid id) => Task.FromResult(Profiles.FirstOrDefault(p => p.Id == id));
    public Task<UserProfile?> GetByUserIdAsync(Guid userId) => Task.FromResult(Profiles.FirstOrDefault(p => p.UserAccountId == userId));
    public Task AddAsync(UserProfile profile) { Profiles.Add(profile); return Task.CompletedTask; }
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

public class InMemoryCorSubmissionRepository : ICorSubmissionRepository
{
    public List<CorSubmission> Submissions { get; } = new();

    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id) => Task.FromResult(Submissions.FirstOrDefault(s => s.Id == id));
    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userAccountId, string academicTerm) =>
        Task.FromResult(Submissions.FirstOrDefault(s => s.UserAccountId == userAccountId && s.AcademicTerm == academicTerm));
    public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync() => Task.FromResult<IEnumerable<CorSubmission>>(Submissions);
    public Task AddCorSubmissionAsync(CorSubmission corSubmission) { Submissions.Add(corSubmission); return Task.CompletedTask; }
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
    public Task DeleteCorSubmissionAsync(CorSubmission corSubmission) { Submissions.Remove(corSubmission); return Task.CompletedTask; }
}

public class UploadFileSpecificHandlersTests
{
    private readonly FakeCloudinaryService _cloudinaryService;
    private readonly InMemoryUserProfileRepository _userProfileRepository;
    private readonly InMemoryCorSubmissionRepository _corSubmissionRepository;

    private readonly UploadProfilePictureCommandValidator _profileValidator;
    private readonly UploadCorDocumentCommandValidator _corValidator;

    public UploadFileSpecificHandlersTests()
    {
        _cloudinaryService = new FakeCloudinaryService();
        _userProfileRepository = new InMemoryUserProfileRepository();
        _corSubmissionRepository = new InMemoryCorSubmissionRepository();

        _profileValidator = new UploadProfilePictureCommandValidator();
        _corValidator = new UploadCorDocumentCommandValidator();
    }

    private IFormFile CreateMockFile(string fileName, string content)
    {
        return new MockFormFile(fileName, content);
    }

    [Fact]
    public async Task UploadProfilePictureHandler_ShouldSuccessfullyUpload_AndUpdateProfile_AndAutoDeletePrevious()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var previousUrl = "https://res.cloudinary.com/test/image/upload/v1234/parkflow/profiles/old_image.jpg";
        
        var profile = new UserProfile(userId, "John", "Doe", null, previousUrl);
        await _userProfileRepository.AddAsync(profile);

        var file = CreateMockFile("new_avatar.png", "fake-avatar-bytes");
        var command = new UploadProfilePictureCommand(file, userId);
        var handler = new UploadProfilePictureHandler(_userProfileRepository, _cloudinaryService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(_cloudinaryService.ExpectedUrl, result.Data.SecureUrl);
        
        // Assert database was updated
        var updatedProfile = await _userProfileRepository.GetByUserIdAsync(userId);
        Assert.NotNull(updatedProfile);
        Assert.Equal(_cloudinaryService.ExpectedUrl, updatedProfile.ProfilePictureUrl);

        // Assert auto-deletion of previous was triggered
        Assert.Contains("parkflow/profiles/old_image", _cloudinaryService.DeletedPublicIds);
    }

    [Fact]
    public async Task UploadCorDocumentHandler_ShouldSuccessfullyUpload_AndUpdateCorSubmission_AndAutoDeletePrevious()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var previousUrl = "https://res.cloudinary.com/test/raw/upload/v1234/parkflow/cor/old_doc.pdf";

        var corSubmission = new CorSubmission(userId, "2025-2026 1st", previousUrl);
        // Force the ID since constructor generates Guid
        var idField = typeof(BaseEntity).GetProperty("Id");
        idField?.SetValue(corSubmission, submissionId);

        await _corSubmissionRepository.AddCorSubmissionAsync(corSubmission);

        var file = CreateMockFile("new_cor.pdf", "fake-pdf-bytes");
        var command = new UploadCorDocumentCommand(file, submissionId);
        var handler = new UploadCorDocumentHandler(_corSubmissionRepository, _cloudinaryService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(_cloudinaryService.ExpectedPdfUrl, result.Data.SecureUrl);

        // Assert database was updated
        var updatedCor = await _corSubmissionRepository.GetCorSubmissionAsync(submissionId);
        Assert.NotNull(updatedCor);
        Assert.Equal(_cloudinaryService.ExpectedPdfUrl, updatedCor.CorDocumentUrl);

        // Assert auto-deletion of previous was triggered
        Assert.Contains("parkflow/cor/old_doc", _cloudinaryService.DeletedPublicIds);
    }

    [Fact]
    public async Task UploadProfilePictureHandler_ShouldReturnNotFoundIfProfileDoesNotExist()
    {
        // Arrange
        var file = CreateMockFile("avatar.jpg", "fake-avatar-bytes");
        var command = new UploadProfilePictureCommand(file, Guid.NewGuid());
        var handler = new UploadProfilePictureHandler(_userProfileRepository, _cloudinaryService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UploadCorDocumentHandler_ShouldReturnNotFoundIfSubmissionDoesNotExist()
    {
        // Arrange
        var file = CreateMockFile("cor.pdf", "fake-pdf-bytes");
        var command = new UploadCorDocumentCommand(file, Guid.NewGuid());
        var handler = new UploadCorDocumentHandler(_corSubmissionRepository, _cloudinaryService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public void ProfileValidator_ShouldFailWhenFileHasInvalidExtension()
    {
        // Arrange
        var file = CreateMockFile("avatar.gif", "fake-avatar-bytes");
        var command = new UploadProfilePictureCommand(file, Guid.NewGuid());

        // Act
        var result = _profileValidator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Invalid image file format"));
    }

    [Fact]
    public void CorValidator_ShouldFailWhenFileIsNotPdfOrImage()
    {
        // Arrange
        var file = CreateMockFile("cor.txt", "fake-bytes");
        var command = new UploadCorDocumentCommand(file, Guid.NewGuid());

        // Act
        var result = _corValidator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Only PDF and image formats (JPG, JPEG, PNG) are allowed"));
    }
}

public class MockFormFile : IFormFile
{
    private readonly byte[] _bytes;
    public string ContentType { get; set; } = "application/octet-stream";
    public string ContentDisposition { get; set; } = "";
    public IHeaderDictionary Headers { get; set; } = null!;
    public long Length => _bytes.Length;
    public string Name { get; }
    public string FileName { get; }

    public MockFormFile(string fileName, string content)
    {
        _bytes = Encoding.UTF8.GetBytes(content);
        FileName = fileName;
        Name = "file";
    }

    public Stream OpenReadStream() => new MemoryStream(_bytes);
    public void CopyTo(Stream target) => new MemoryStream(_bytes).CopyTo(target);
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => new MemoryStream(_bytes).CopyToAsync(target, cancellationToken);
}
