using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.Verification;
using System.Text.Json;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Features.Files.Commands.UploadCorDocument;
using Test.Features.Files;

namespace Test.Features.Verification;

public class PostgresVerificationFactAttribute : FactAttribute
{
    public PostgresVerificationFactAttribute()
    {
        if (!IsDisposableLocal(Environment.GetEnvironmentVariable("PARKFLOW_VERIFICATION_TEST_CONNECTION")))
            Skip = "Requires a disposable local *_verification_test database via PARKFLOW_VERIFICATION_TEST_CONNECTION.";
    }

    public static bool IsDisposableLocal(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) return false;
        try
        {
            var target = new NpgsqlConnectionStringBuilder(connection);
            return target.Host is "localhost" or "127.0.0.1" &&
                target.Database?.EndsWith("_verification_test", StringComparison.Ordinal) == true;
        }
        catch (ArgumentException) { return false; }
    }
}

[Collection("Parking deadline settings")]
public class VerificationStoreTests
{
    [PostgresVerificationFact]
    public async Task EmployeeScheduleHasAnIndependentAuditedApprovalAndVehicleApprovalCannotReplaceIt()
    {
        await using var fixture=await DatabaseFixture.Create();var (user,cor)=await fixture.Seed(personnel:true);
        await using var db=fixture.Context();var store=new VerificationStore(db);var actor=Guid.NewGuid();
        var profile=await db.UserProfiles.SingleAsync(p=>p.UserAccountId==user.Id);
        profile.ReviewLicense(true,actor,"Synthetic license checked");
        var identity=AuthIdentity.CreateManual(user.Id,"staff@example.invalid","synthetic",true);identity.MarkVerified();
        var vehicle=new Vehicle(user.Id,"STAFF"+Guid.NewGuid().ToString("N")[..6],"Synthetic","hash",VehicleType.Car);
        vehicle.UpdateDocuments("uploads/orcr.pdf","uploads/vehicle.png");vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        db.AddRange(identity,vehicle,new ParkingSchedule(cor.Id,DayOfWeek.Monday,TimeSpan.FromHours(8),TimeSpan.FromHours(10)));await db.SaveChangesAsync();
        var account=(await store.EnqueueAsync(VerificationType.Account,user.Id,default))!;
        Assert.True(await store.ReviewAsync(account.Id,account.Version,account.Fingerprint,new(VerificationState.Approved,"Identity checked"),actor,default));
        var document=(await store.EnqueueAsync(VerificationType.Cor,cor.Id,default))!;
        Assert.True(await store.ReviewAsync(document.Id,document.Version,document.Fingerprint,new(VerificationState.Approved,"Employee document checked"),actor,default));
        (await db.CorSubmissions.FindAsync(cor.Id))!.SubmitEmployeeSchedule("uploads/employee-schedule.pdf");await db.SaveChangesAsync();
        var schedule=(await store.EnqueueAsync(VerificationType.Schedule,cor.Id,default))!;
        Assert.Equal(VerificationState.NeedsReview,schedule.Status);
        Assert.Equal(AccountStatus.PendingVerification,(await db.UserAccounts.AsNoTracking().SingleAsync(u=>u.Id==user.Id)).Status);
        Assert.True(await store.ReviewAsync(schedule.Id,schedule.Version,schedule.Fingerprint,new(VerificationState.Approved,"Uploaded employee schedule checked"),actor,default));
        Assert.Equal(AccountStatus.Active,(await db.UserAccounts.AsNoTracking().SingleAsync(u=>u.Id==user.Id)).Status);
        Assert.Contains(await store.ListAsync(user.Id,false,default),r=>r.Type==VerificationType.Schedule);
        Assert.Contains(await store.AuditAsync(schedule.Id,default),audit=>audit.Action=="ManualReview" && audit.ActorId==actor);
        var current=(await store.FindAsync(schedule.Id,default))!;
        Assert.True(await store.ReviewAsync(current.Id,current.Version,current.Fingerprint,new(VerificationState.Rejected,"Replacement employee schedule required"),actor,default));
        Assert.Equal(AccountStatus.PendingVerification,(await db.UserAccounts.AsNoTracking().SingleAsync(u=>u.Id==user.Id)).Status);
    }
    [PostgresVerificationFact]
    public async Task StudentRestorationPersistsCurrentDocumentApprovalWithoutBypassingPendingDocuments()
    {
        await using var fixture = await DatabaseFixture.Create();
        await using var db = fixture.Context();
        var user = new UserAccount("hash", null);
        var identity = AuthIdentity.CreateManual(user.Id, "restore@example.test", "hash", true); identity.MarkVerified();
        var profile = new UserProfile(user.Id, "Juan", "Dela Cruz", null, "uploads/profile.jpg");
        profile.SubmitLicense("uploads/license.jpg"); profile.ReviewLicense(true, Guid.NewGuid(), "Checked");
        var student = new Student(profile.Id, "2023106763", "BSCS", "4A", 4);
        var vehicle = new Vehicle(user.Id, "RST1234", "Honda", "restore-hash", VehicleType.Car,
            orcrDocumentUrl: "uploads/orcr.pdf", vehiclePictureUrl: "uploads/vehicle.jpg");
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified, "Checked");
        user.UpdateStatus(AccountStatus.Suspended);
        db.AddRange(user, identity, profile, student, vehicle); await db.SaveChangesAsync();
        var store = new VerificationStore(db);
        Assert.Equal(AccountStatus.Active, await store.RestoreAccountAsync(user.Id, default));
        await using (var fresh = fixture.Context())
            Assert.Equal(AccountStatus.Active, (await fresh.UserAccounts.SingleAsync()).Status);

        var tracked = (await db.UserAccounts.FindAsync(user.Id))!; tracked.UpdateStatus(AccountStatus.Suspended);
        var changed = (await db.Vehicles.FindAsync(vehicle.Id))!; changed.UpdateVerificationStatus(CorVerificationStatus.Pending, null);
        await db.SaveChangesAsync();
        Assert.Equal(AccountStatus.PendingVerification, await store.RestoreAccountAsync(user.Id, default));
        await using var refreshed = fixture.Context();
        Assert.Equal(AccountStatus.PendingVerification, (await refreshed.UserAccounts.SingleAsync()).Status);
        Assert.Equal(CorVerificationStatus.Pending, (await refreshed.Vehicles.SingleAsync()).VerificationStatus);
    }

    [PostgresVerificationFact]
    public async Task DigitalUploadReturnsOwnedPreviewWithoutDownloadOcrOrAccountApproval()
    {
        await using var fixture = await DatabaseFixture.Create();
        await using var db = fixture.Context(); var user = new UserAccount("hash", null);
        var identity = AuthIdentity.CreateManual(user.Id, "student@example.test", "hash", true); identity.MarkVerified(); db.AddRange(user, identity); await db.SaveChangesAsync();
        var store = new VerificationStore(db); var reader = new PreparedReader();
        var cloud = new FakeCloudinaryService(); var service = new VerificationService(store, reader, cloud, new NoTrustedApproval(), null!, null!, new NoCache());
        var handler = new UploadCorDocumentHandler(new CorSubmissionRepository(db), cloud, new FakeUserContext { UserId = user.Id },
            documentReader: reader, verificationStore: store, verificationService: service);
        using var stream = new MemoryStream(DocumentReaderTests.DigitalPdf()); var response = await handler.Handle(new(new FormFile(stream, 0, stream.Length, "File", "cor.pdf")), default);
        Assert.True(response.IsSuccess); Assert.NotNull(response.Data!.CorPreview); Assert.False(response.Data.CorPreview.CanConfirm);
        Assert.Equal(7, response.Data.CorPreview.Week.Count); Assert.Empty(response.Data.CorPreview.Items);
        Assert.Equal(0, reader.Preflights); Assert.Equal(1, reader.Validations); Assert.Equal(0, reader.OcrReads);
        Assert.Equal(AccountStatus.PendingVerification, (await db.UserAccounts.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(OnboardingStep.Profile, (await db.UserAccounts.AsNoTracking().SingleAsync()).OnboardingStep);
        Assert.Empty(await db.Students.ToListAsync()); Assert.Empty(await db.ParkingSchedules.ToListAsync());
        var record = (await store.FindCurrentCorAsync(user.Id, default))!; Assert.NotEqual(VerificationState.Approved, record.Status); Assert.Equal(0, record.Attempts); Assert.Equal(VerificationRequests.AwaitingInformationFetch, record.OcrOutcome); Assert.Null(await store.ClaimAsync(TimeSpan.FromMinutes(1), default));
        await service.ProcessPreparedCorAsync(user.Id, record.SubjectId, response.Data.SecureUrl, default);
        Assert.Equal(0, (await store.FindCurrentCorAsync(user.Id, default))!.Attempts); Assert.Equal(0, reader.OcrReads);
        Assert.Null(await new VerificationRequests(store, service).Handle(new GetCorSchedulePreview(Guid.NewGuid()), default));
    }

    [PostgresVerificationFact]
    public async Task PersistedDigitalExtractionIsAvailableBeforeConcurrentWorkerClaims()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, _) = await fixture.Seed();
        await using (var db = fixture.Context())
        {
            var cor = (await db.CorSubmissions.SingleAsync());
            var fields = CorTextVerification.Extract(VerificationRulesTests.Text + "\n" + CorScheduleTests.ScheduleTable);
            await new CorSubmissionRepository(db).SaveUploadedAsync(cor, false, fields, "DigitalPdfTextExtracted", default);
        }
        var claims = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        { await using var db = fixture.Context(); return await new VerificationStore(db).ClaimAsync(TimeSpan.FromMinutes(1), default); }));
        var claim = Assert.Single(claims, c => c != null)!;
        Assert.NotNull(claim.ExtractedFieldsJson); Assert.Equal("DigitalPdfTextExtracted", claim.OcrOutcome);
        Assert.Equal("uploads/parkflow/cor/original.pdf", JsonSerializer.Deserialize<CorFields>(claim.ExtractedFieldsJson)!.SourceDocumentUrl);
    }

    [PostgresVerificationFact]
    public async Task FailedInlineDigitalWorkReturnsLeaseWithoutConsumingBackgroundAttemptsOrOverwritingReview()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var fields = CorTextVerification.Extract(VerificationRulesTests.Text + "\n" + CorScheduleTests.ScheduleTable);
        await new CorSubmissionRepository(db).SaveUploadedAsync(cor, false, fields, "DigitalPdfTextExtracted", default);
        var claim = (await store.ClaimCorAsync(cor.Id, TimeSpan.FromMinutes(1), default))!;
        await store.DeferPreparedCorAsync(claim, default);
        var deferred = (await store.FindCurrentCorAsync(user.Id, default))!;
        Assert.Equal(VerificationState.Pending, deferred.Status); Assert.Equal(0, deferred.Attempts); Assert.Null(deferred.LeaseId);
        var next = (await store.ClaimAsync(TimeSpan.FromMinutes(1), default))!; Assert.Equal(1, next.Attempts);
        Assert.True(await store.ReviewAsync(next.Id, next.Version, next.Fingerprint, new(VerificationState.Rejected, "Manually rejected"), Guid.NewGuid(), default));
        await store.DeferPreparedCorAsync(next, default);
        Assert.Equal(VerificationState.Rejected, (await store.FindCurrentCorAsync(user.Id, default))!.Status);
    }

    [PostgresVerificationFact]
    public async Task BothPersonnelRolesKeepEmployeeDocumentBackgroundProcessingWithoutStudentPreview()
    {
        await using var fixture = await DatabaseFixture.Create();
        foreach (var role in new[] { Roles.UniversityStaff, Roles.NonAcademicPersonnel })
        {
            await using var db = fixture.Context(); var user = new UserAccount("hash", "09171234567");
            var profile = new UserProfile(user.Id, "Faculty", "Example", null, null); db.AddRange(user, profile, new Personnel(profile.Id, "EMP-" + (int)role, "CICT", role)); await db.SaveChangesAsync();
            var store = new VerificationStore(db); var reader = new PreparedReader(); var cloud = new FakeCloudinaryService();
            var service = new VerificationService(store, reader, cloud, new NoTrustedApproval(), null!, null!, new NoCache());
            var handler = new UploadCorDocumentHandler(new CorSubmissionRepository(db), cloud, new FakeUserContext { UserId = user.Id },
                documentReader: reader, verificationStore: store, verificationService: service);
            using var stream = new MemoryStream(DocumentReaderTests.DigitalPdf());
            var response = await handler.Handle(new(new FormFile(stream, 0, stream.Length, "File", "cor.pdf")), default);
            Assert.True(response.IsSuccess); Assert.Null(response.Data!.CorPreview); Assert.Equal(0, reader.Preflights); Assert.Equal(1, reader.Validations);
            var record = (await store.FindCurrentCorAsync(user.Id, default))!;
            Assert.Equal(VerificationState.Pending, record.Status); Assert.Equal(0, record.Attempts); Assert.Null(record.ExtractedFieldsJson);
            Assert.Null(await store.ClaimCorAsync(record.SubjectId, TimeSpan.FromMinutes(1), default));
        }
    }

    private sealed class PreparedReader : ICorDocumentReader
    {
        public int Preflights { get; private set; } public int OcrReads { get; private set; } public int Validations { get; private set; }
        public bool IsStoredDocument(string url) => true;
        public Task ValidateAsync(Stream stream, string fileName, CancellationToken ct) { Validations++; ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public Task<DocumentText?> ReadTextLayerAsync(Stream stream, string fileName, CancellationToken ct)
        { Preflights++; ct.ThrowIfCancellationRequested(); return Task.FromResult<DocumentText?>(new(VerificationRulesTests.Text + "\n" + CorScheduleTests.ScheduleTable, "DigitalPdfTextExtracted")); }
        public Task<DocumentText> ReadAsync(Stream stream, string fileName, CancellationToken ct) { OcrReads++; throw new InvalidOperationException("Second processing must not run."); }
    }
    [CorPerformanceFact]
    public async Task MeasureDigitalServerPipelineAndPreviewWithRealPostgres()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PARKFLOW_VERIFICATION_TEST_CONNECTION"))) return;
        var bytes = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("PARKFLOW_COR_SAMPLE")!);
        await using var fixture = await DatabaseFixture.Create(); var timings = new List<double>(); var previews = new List<double>();
        for (var run = 0; run < 7; run++)
        {
            await using var db = fixture.Context(); var account = new UserAccount("hash", null); var identity = AuthIdentity.CreateManual(account.Id, Guid.NewGuid() + "@example.test", "hash", true); identity.MarkVerified();
            db.AddRange(account, identity); await db.SaveChangesAsync();
            var store = new VerificationStore(db); var reader = DocumentReaderTests.Reader(); var cloud = new PerformanceCloud(bytes);
            var service = new VerificationService(store, reader, cloud, new NoTrustedApproval(), null!, null!, new NoCache());
            var services = new ServiceCollection().AddSingleton<ICorSubmissionRepository>(new CorSubmissionRepository(db))
                .AddSingleton<ICloudinaryService>(cloud).AddSingleton<IUserContext>(new FakeUserContext { UserId = account.Id })
                .AddSingleton<FluentValidation.IValidator<UploadCorDocumentCommand>>(new UploadCorDocumentCommandValidator())
                .AddSingleton<ICorDocumentReader>(reader).AddSingleton<IVerificationStore>(store).AddSingleton(service);
            using var provider = services.BuildServiceProvider(); var handler = ActivatorUtilities.CreateInstance<UploadCorDocumentHandler>(provider);
            using var stream = new MemoryStream(bytes, false); var file = new FormFile(stream, 0, bytes.Length, "File", "cor.pdf");
            var watch = Stopwatch.StartNew(); var response = await handler.Handle(new(file), default); Assert.True(response.IsSuccess);
            await service.EnqueueAsync(VerificationType.Cor, response.Data!.SubmissionId!.Value, default);
            var claim = await store.ClaimAsync(TimeSpan.FromMinutes(2), default);
            if (claim != null) await service.ProcessAsync(claim, default);
            var preview = await new VerificationRequests(store, service).Handle(new GetCorSchedulePreview(account.Id), default);
            if (run > 0) timings.Add(watch.Elapsed.TotalMilliseconds);
            Assert.True(preview!.CanConfirm); Assert.Equal(7, preview.Week.Count); Assert.Equal(4, preview.Items.Count);
            watch.Restart(); await new VerificationRequests(store, service).Handle(new GetCorSchedulePreview(account.Id), default);
            if (run > 0) previews.Add(watch.Elapsed.TotalMilliseconds);
        }
        timings.Sort(); previews.Sort();
        var json = JsonSerializer.Serialize(new { Runs = 6, Storage = "InMemoryNoNetwork", Database = "LocalPostgreSQL", QueueWaitExcluded = true,
            CompleteDigitalPipelineMedianMs = Math.Round((timings[2] + timings[3]) / 2, 3), PreviewMedianMs = Math.Round((previews[2] + previews[3]) / 2, 3) });
        var destination = Environment.GetEnvironmentVariable("PARKFLOW_COR_PERFORMANCE_API_OUTPUT");
        if (!string.IsNullOrWhiteSpace(destination)) await File.WriteAllTextAsync(destination, json);
    }

    private sealed class PerformanceCloud(byte[] bytes) : ICloudinaryService
    {
        public Task<(string SecureUrl, string PublicId)> UploadPdfAsync(IFormFile file, string folder) => Task.FromResult(("uploads/parkflow/cor/" + Guid.NewGuid() + ".pdf", "cor/test"));
        public Task<(string SecureUrl, string PublicId)> UploadImageAsync(IFormFile file, string folder) => throw new InvalidOperationException();
        public Task DeleteFileAsync(string publicId, bool isImage = true) => Task.CompletedTask;
        public string? GetAuthenticatedDownloadUrl(string fileUrlOrPublicId, bool attachment = false) => null;
        public Task<(Stream Stream, string ContentType, string FileName)?> GetDocumentStreamAsync(string fileUrlOrPublicId, bool attachment = false) =>
            Task.FromResult<(Stream Stream, string ContentType, string FileName)?>((new MemoryStream(bytes, false), "application/pdf", "cor.pdf"));
    }
    private static CorStudentConfirmation StudentConfirmation(VerificationRecord record, List<CorWeekDay>? week = null) => new(record.Id, record.Version,
        new("Juan Miguel", "Example", "R.", "09171234567", "2023000001", "CICT", "BS Information Technology (BSIT)", 4, "4A", "2026-2027", "1st Semester"),
        week ?? CorStudentMapping.Week(CorTextVerification.Extract(CorScheduleTests.ScheduleTable)));

    private static async Task<VerificationRecord> ScannedStudent(DatabaseFixture fixture, Guid owner, Guid corId, bool verified = true)
    {
        await using var db = fixture.Context();
        var user = (await db.UserAccounts.FindAsync(owner))!; user.UpdateStatus(AccountStatus.PendingVerification);
        var identity = AuthIdentity.CreateManual(owner, Guid.NewGuid() + "@example.test", "hash", true);
        if (verified) identity.MarkVerified(); db.Add(identity); await db.SaveChangesAsync();
        var store = new VerificationStore(db); await store.EnqueueAsync(VerificationType.Cor, corId, default);
        var claim = (await store.ClaimAsync(TimeSpan.FromMinutes(2), default))!;
        var fields = CorTextVerification.Extract(VerificationRulesTests.Text + "\n" + CorScheduleTests.ScheduleTable) with { SourceDocumentUrl = "uploads/parkflow/cor/original.pdf" };
        Assert.True(await store.CompleteAsync(claim, new(VerificationState.NeedsReview, "Complete Student profile before identity review", "DigitalPdfTextExtracted", JsonSerializer.Serialize(fields)), default));
        return (await store.FindAsync(claim.Id, default))!;
    }

    [PostgresVerificationFact]
    public async Task StudentConfirmationSavesExistingEntitiesAtomicallyWithoutApprovingAccount()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        var record = await ScannedStudent(fixture, user.Id, cor.Id);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var request = StudentConfirmation(record);
        Assert.NotNull(request.Week);
        request.Week[0] = request.Week[0] with { StartTime = "06:30:00" };
        Assert.False(await store.ConfirmStudentFromCorAsync(Guid.NewGuid(), request, default));
        Assert.False(await store.ConfirmStudentFromCorAsync(user.Id, StudentConfirmation(record) with { Version = record.Version - 1 }, default));
        Assert.Empty(await db.UserProfiles.ToListAsync()); Assert.Empty(await db.ParkingSchedules.ToListAsync());
        await Assert.ThrowsAsync<DocumentValidationException>(() => store.ConfirmStudentFromCorAsync(user.Id, request, default));
        request = StudentConfirmation(record);
        Assert.True(await store.ConfirmStudentFromCorAsync(user.Id, request, default));
        var savedUser = await db.UserAccounts.AsNoTracking().Include(u => u.UserProfile).ThenInclude(p => p!.Student).SingleAsync();
        Assert.Equal(AccountStatus.PendingVerification, savedUser.Status); Assert.Equal(OnboardingStep.Vehicle, savedUser.OnboardingStep);
        Assert.Equal("09171234567", savedUser.PhoneNumber); Assert.Equal("Juan Miguel", savedUser.UserProfile!.FirstName);
        Assert.Equal("R.", savedUser.UserProfile.MiddleName); Assert.Equal("BS Information Technology (BSIT)", savedUser.UserProfile.Student!.Course);
        Assert.Equal(4, await db.ParkingSchedules.CountAsync());
        Assert.Equal(TimeSpan.FromHours(7), (await db.ParkingSchedules.AsNoTracking().SingleAsync(s => s.DayOfWeek == DayOfWeek.Monday)).StartTime);
        Assert.Equal(CorVerificationStatus.Verified, (await db.CorSubmissions.AsNoTracking().SingleAsync()).VerificationStatus);
        Assert.DoesNotContain(await store.ListAsync(user.Id, false, default), r => r.Status == VerificationState.Approved);
        var count = await db.VerificationAudits.CountAsync();
        Assert.True(await store.ConfirmStudentFromCorAsync(user.Id, request, default)); Assert.Equal(count, await db.VerificationAudits.CountAsync());
        var cached = await store.FindCorExtractionByDocumentAsync(user.Id, cor.CorDocumentUrl, default);
        Assert.NotNull(cached); Assert.Equal("DigitalPdfTextExtracted", cached.OcrOutcome);
        Assert.Null(await store.FindCorExtractionByDocumentAsync(user.Id, "uploads/parkflow/cor/different.pdf", default));
        var preview = (await new VerificationRequests(store, null!).Handle(new GetCorSchedulePreview(user.Id), default))!;
        Assert.True(preview.InformationConfirmed); Assert.Equal("CICT", preview.StudentInformation!.College);
        Assert.Equal("07:00:00", preview.Week[0].StartTime); Assert.Equal("1", CorTextVerification.SemesterKey(preview.StudentInformation.Semester));
        var current = (await store.FindCurrentCorAsync(user.Id, default))!;
        var changed = StudentConfirmation(current) with { Information = StudentConfirmation(current).Information with { Section = "4B" }, Version = current.Version - 1 };
        Assert.False(await store.ConfirmStudentFromCorAsync(user.Id, changed, default));
    }

    [PostgresVerificationFact]
    public async Task StudentConfirmationRejectsUnverifiedEmailPersonnelDuplicateNumbersAndUnknownDaysWithoutPartialWrites()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        var record = await ScannedStudent(fixture, user.Id, cor.Id, verified: false);
        await using var db = fixture.Context(); var store = new VerificationStore(db); var request = StudentConfirmation(record);
        Assert.Contains("email", (await Assert.ThrowsAsync<DocumentValidationException>(() => store.ConfirmStudentFromCorAsync(user.Id, request, default))).Message);
        (await db.AuthIdentities.SingleAsync()).MarkVerified(); await db.SaveChangesAsync();
        await store.EnqueueAsync(VerificationType.Cor, cor.Id, default);
        var refreshed = (await store.ClaimAsync(TimeSpan.FromMinutes(2), default))!;
        Assert.True(await store.CompleteAsync(refreshed, new(VerificationState.NeedsReview, "Awaiting Student review", "DigitalPdfTextExtracted", record.ExtractedFieldsJson), default));
        request = StudentConfirmation((await store.FindAsync(refreshed.Id, default))!);
        var other = new UserAccount("hash", null); var otherProfile = new UserProfile(other.Id, "Other", "Student", null, null);
        db.AddRange(other, otherProfile, new Student(otherProfile.Id, request.Information.StudentNumber, "BSIT", "4B", 4)); await db.SaveChangesAsync();
        Assert.Contains("already registered", (await Assert.ThrowsAsync<DocumentValidationException>(() => store.ConfirmStudentFromCorAsync(user.Id, request, default))).Message);
        Assert.Equal(1, await db.UserProfiles.CountAsync()); Assert.Empty(await db.ParkingSchedules.ToListAsync());
        var ownProfile = new UserProfile(user.Id, "Faculty", "Example", null, null);
        db.AddRange(ownProfile, new Personnel(ownProfile.Id, "EMP-123", "CICT", Roles.UniversityStaff)); await db.SaveChangesAsync();
        Assert.Contains("only available to Student", (await Assert.ThrowsAsync<DocumentValidationException>(() => store.ConfirmStudentFromCorAsync(user.Id, request, default))).Message);
        Assert.Empty(await db.ParkingSchedules.ToListAsync());
        Assert.Equal("Faculty", (await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserAccountId == user.Id)).FirstName);
    }

    [PostgresVerificationFact]
    public async Task ConcurrentStudentConfirmationCreatesOneProfileAndOneSetOfDailyWindows()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        var record = await ScannedStudent(fixture, user.Id, cor.Id); var request = StudentConfirmation(record);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        { await using var db = fixture.Context(); return await new VerificationStore(db).ConfirmStudentFromCorAsync(user.Id, request, default); }));
        Assert.All(results, Assert.True);
        await using var verify = fixture.Context(); Assert.Equal(1, await verify.Students.CountAsync()); Assert.Equal(4, await verify.ParkingSchedules.CountAsync());
        Assert.Equal(0, await verify.VerificationAudits.CountAsync(a => a.Action == "StudentInformationConfirmed"));
    }

    [PostgresVerificationFact]
    public async Task ConfirmedStudentRevisionReusesExtractionWithoutNewWorkerOrOcr()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        var record = await ScannedStudent(fixture, user.Id, cor.Id);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        Assert.True(await store.ConfirmStudentFromCorAsync(user.Id, StudentConfirmation(record), default));
        var completed = (await store.FindCurrentCorAsync(user.Id, default))!;
        var reader = new NoSecondScan();
        var service = new VerificationService(store, reader, null!, new NoTrustedApproval(), null!, null!, new NoCache());
        await service.ProcessPreparedCorAsync(user.Id, cor.Id, cor.CorDocumentUrl, default);
        Assert.Equal(VerificationState.Approved, completed.Status);
        Assert.Equal("DigitalPdfTextExtracted", completed.OcrOutcome);
        Assert.NotNull(JsonSerializer.Deserialize<CorFields>(completed.ExtractedFieldsJson!)!.ConfirmedInformation);
        Assert.Null(await store.ClaimAsync(TimeSpan.FromMinutes(2), default));
        Assert.Equal(0, reader.Reads);
    }

    private sealed class NoSecondScan : ICorDocumentReader
    {
        public int Reads { get; private set; }
        public bool IsStoredDocument(string url) => true;
        public Task ValidateAsync(Stream stream, string fileName, CancellationToken ct) => throw new InvalidOperationException("No second validation expected.");
        public Task<DocumentText> ReadAsync(Stream stream, string fileName, CancellationToken ct)
        { Reads++; throw new InvalidOperationException("No second OCR expected."); }
    }
    private sealed class NoTrustedApproval : IInstitutionalVerificationProvider
    {
        public Task<TrustedVerificationEvidence> VerifyAsync(VerificationSubject subject, CancellationToken ct) => Task.FromResult(new TrustedVerificationEvidence(false, null, subject.Fingerprint));
    }
    private sealed class NoCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => factory();
    }

    [PostgresVerificationFact]
    public async Task UnreadableScheduleCannotBeManuallyCorrectedAndRejectedOrReplacedCorCannotBeConfirmed()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        var record = await ScannedStudent(fixture, user.Id, cor.Id);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var originalFields = record.ExtractedFieldsJson;
        var tracked = (await db.VerificationRecords.FindAsync(record.Id))!;
        tracked.ExtractedFieldsJson = JsonSerializer.Serialize(CorTextVerification.Extract(VerificationRulesTests.Text + "\nUnknown 08:00-10:00") with { SourceDocumentUrl = cor.CorDocumentUrl }); await db.SaveChangesAsync();
        var preview = (await new VerificationRequests(store, null!).Handle(new GetCorSchedulePreview(user.Id), default))!;
        Assert.False(preview.CanConfirmSchedule);
        await Assert.ThrowsAsync<DocumentValidationException>(() => store.ConfirmStudentFromCorAsync(user.Id, StudentConfirmation(record, preview.Week), default));
        Assert.Empty(await db.Students.ToListAsync()); Assert.Empty(await db.ParkingSchedules.ToListAsync());
        tracked = (await db.VerificationRecords.FindAsync(record.Id))!;
        tracked.ExtractedFieldsJson = originalFields; await db.SaveChangesAsync();
        Assert.True(await store.ReviewAsync(record.Id, record.Version, record.Fingerprint, new(VerificationState.Rejected, "Unreadable document"), Guid.NewGuid(), default));
        Assert.False(await store.ConfirmStudentFromCorAsync(user.Id, StudentConfirmation(record), default));
        var currentCor = (await db.CorSubmissions.FindAsync(cor.Id))!;
        currentCor.UpdateSubmission(null, "uploads/parkflow/cor/new.pdf", CorVerificationStatus.Pending); await new CorSubmissionRepository(db).UpdateCorSubmissionAsync(currentCor);
        Assert.False(await store.ConfirmStudentFromCorAsync(user.Id, StudentConfirmation(record), default));
    }

    [PostgresVerificationFact]
    public async Task VehicleDocumentsStayManualWithoutNewVerificationRecordsOrAudits()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, _) = await fixture.Seed(driver: true);
        await using var db = fixture.Context(); var store = new VerificationStore(db); var vehicles = new VehicleRepository(db);
        var vehicle = new Vehicle(user.Id, "ABC1234", "Honda", "hash", VehicleType.Motorcycle,
            orcrDocumentUrl: "uploads/parkflow/orcr/registration.pdf", vehiclePictureUrl: "uploads/parkflow/motor-pictures/plate.jpg");
        await vehicles.AddAsync(vehicle);
        Assert.Null(await store.EnqueueAsync(VerificationType.Vehicle, vehicle.Id, default));
        Assert.Empty(await db.VerificationRecords.ToListAsync()); Assert.Empty(await db.VerificationAudits.ToListAsync());
        var source = (await store.GetSubjectAsync(VerificationType.Vehicle, vehicle.Id, default))!;
        Assert.True(await store.ReviewVehicleManuallyAsync(vehicle.Id, source.Fingerprint, VerificationState.Approved, "OR/CR and plate checked", Guid.NewGuid(), default));
        Assert.Equal(CorVerificationStatus.Verified, (await vehicles.GetByIdAsync(vehicle.Id))!.VerificationStatus);
        Assert.True(await store.ReviewVehicleManuallyAsync(vehicle.Id, source.Fingerprint, VerificationState.Rejected, "Plate mismatch", Guid.NewGuid(), default));
        Assert.Equal(AccountStatus.Active, (await db.UserAccounts.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await db.VerificationRecords.ToListAsync()); Assert.Empty(await db.VerificationAudits.ToListAsync());
        var changed = (await vehicles.GetByIdAsync(vehicle.Id))!;
        changed.UpdateDocuments(null, "uploads/parkflow/motor-pictures/replacement.jpg"); await vehicles.UpdateAsync(changed);
        Assert.Equal(CorVerificationStatus.Pending, changed.VerificationStatus);
        Assert.False(await store.ReviewVehicleManuallyAsync(vehicle.Id, source.Fingerprint, VerificationState.Approved, "Stale document", Guid.NewGuid(), default));
        Assert.Empty(await db.VerificationRecords.ToListAsync()); Assert.Empty(await db.VerificationAudits.ToListAsync());
    }

    [PostgresVerificationFact]
    public async Task OldVehicleJobsCannotProcessOrOverwriteManualReviewAndHistoryIsPreserved()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, _) = await fixture.Seed(driver: true);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var vehicle = new Vehicle(user.Id, "DEF5678", "Honda", "hash", VehicleType.Motorcycle,
            orcrDocumentUrl: "uploads/orcr.pdf", vehiclePictureUrl: "uploads/plate.jpg");
        await new VehicleRepository(db).AddAsync(vehicle);
        var source = (await store.GetSubjectAsync(VerificationType.Vehicle, vehicle.Id, default))!;
        var legacy = new VerificationRecord { Type = VerificationType.Vehicle, SubjectId = vehicle.Id, UserAccountId = user.Id,
            Fingerprint = source.Fingerprint, Status = VerificationState.Processing, LeaseId = Guid.NewGuid(), LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        db.Add(legacy); db.Add(new VerificationAudit { VerificationRecordId = legacy.Id, Action = "LegacyVehicleReview" }); await db.SaveChangesAsync();
        Assert.Null(await store.ClaimAsync(TimeSpan.FromMinutes(1), default));
        Assert.True(await store.ReviewVehicleManuallyAsync(vehicle.Id, source.Fingerprint, VerificationState.Approved, "Checked manually", Guid.NewGuid(), default));
        Assert.False(await store.CompleteAsync(legacy, new(VerificationState.Rejected, "Old worker result") { PolicyVersion = 0 }, default));
        Assert.False((await store.FindAsync(legacy.Id, default))!.IsCurrent);
        Assert.Single(await db.VerificationAudits.ToListAsync());
        Assert.Equal(CorVerificationStatus.Verified, (await db.Vehicles.AsNoTracking().SingleAsync()).VerificationStatus);
    }

    [PostgresVerificationFact]
    public async Task LegacyScheduleReviewIsRetiredWithoutOcrAndHistoryIsPreserved()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var legacy = new VerificationRecord { Type = VerificationType.Schedule, SubjectId = cor.Id, UserAccountId = user.Id,
            Fingerprint = "legacy", Status = VerificationState.NeedsReview, Reason = "Daily schedule exceeds the university parking limit." };
        db.Add(legacy); await db.SaveChangesAsync();
        Assert.Null(await store.EnqueueAsync(VerificationType.Schedule, cor.Id, default));
        var preserved = (await store.FindAsync(legacy.Id, default))!;
        Assert.False(preserved.IsCurrent); Assert.Equal(VerificationState.NeedsReview, preserved.Status);
        Assert.Equal(CorVerificationStatus.Verified, (await db.CorSubmissions.AsNoTracking().SingleAsync()).ScheduleVerificationStatus);
        Assert.Null(await store.ClaimAsync(TimeSpan.FromMinutes(1), default));
        Assert.Single(await db.VerificationRecords.ToListAsync());
    }

    [PostgresVerificationFact]
    public async Task ConfirmingSavedOcrSchedulesIsOwnedVersionedAndDoesNotScanAgain()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        await store.EnqueueAsync(VerificationType.Cor, cor.Id, default);
        var claim = (await store.ClaimAsync(TimeSpan.FromMinutes(2), default))!;
        var fields = CorTextVerification.Extract(VerificationRulesTests.Text + "\n" + CorScheduleTests.ScheduleTable);
        Assert.True(await store.CompleteAsync(claim, new(VerificationState.PreVerified, "Review authenticity", "DigitalPdfTextExtracted", JsonSerializer.Serialize(fields)) { PolicyVersion = 0 }, default));
        var record = (await store.FindAsync(claim.Id, default))!;
        Assert.False(await store.ConfirmExtractedSchedulesAsync(record.Id, record.Version, Guid.NewGuid(), default));
        Assert.False(await store.ConfirmExtractedSchedulesAsync(record.Id, record.Version - 1, user.Id, default));
        Assert.True(await store.ConfirmExtractedSchedulesAsync(record.Id, record.Version, user.Id, default));
        var confirmed = (await store.FindAsync(record.Id, default))!;
        Assert.False(await store.ConfirmExtractedSchedulesAsync(record.Id, record.Version, user.Id, default));
        Assert.True(await store.ConfirmExtractedSchedulesAsync(record.Id, confirmed.Version, user.Id, default));
        Assert.Equal(4, await db.ParkingSchedules.CountAsync());
        Assert.Equal(1, (await store.FindAsync(record.Id, default))!.Attempts);
        Assert.Empty(await store.AuditAsync(record.Id, default));
        db.ChangeTracker.Clear();
        var currentCor = (await new CorSubmissionRepository(db).GetCorSubmissionAsync(cor.Id))!;
        currentCor.UpdateSubmission(null, null, null, "uploads/orcr.pdf", "uploads/motor.jpg");
        await new CorSubmissionRepository(db).UpdateCorSubmissionAsync(currentCor);
        Assert.Single(await db.VerificationRecords.AsNoTracking().Where(r => r.Type == VerificationType.Cor).ToListAsync());
        Assert.Empty(await store.ListAsync(user.Id, true, default));
        currentCor.UpdateSubmission(null, "uploads/parkflow/cor/replacement.pdf", CorVerificationStatus.Pending);
        await new CorSubmissionRepository(db).UpdateCorSubmissionAsync(currentCor);
        Assert.False(await store.ConfirmExtractedSchedulesAsync(record.Id, record.Version, user.Id, default));
    }

    [PostgresVerificationFact]
    public async Task ScheduleParentWithoutDocumentDoesNotBecomeAnOcrSubmission()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed();
        await using var db = fixture.Context();
        await db.CorSubmissions.Where(x => x.Id == cor.Id).ExecuteUpdateAsync(x =>
            x.SetProperty(s => s.CorDocumentUrl, "pending").SetProperty(s => s.VerificationStatus, CorVerificationStatus.NotSubmitted));
        Assert.Null(await new VerificationStore(db).EnqueueAsync(VerificationType.Cor, cor.Id, default));
        Assert.Empty(await db.VerificationRecords.ToListAsync());
        Assert.Equal(CorVerificationStatus.NotSubmitted, (await db.CorSubmissions.AsNoTracking().SingleAsync()).VerificationStatus);
    }

    [PostgresVerificationFact]
    public async Task IdenticalCorUpdateKeepsItsExistingDecisionAndLegacyStatus()
    {
        await using var fixture = await DatabaseFixture.Create(); var (_, cor) = await fixture.Seed(personnel: true);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var record = (await store.EnqueueAsync(VerificationType.Cor, cor.Id, default))!;
        Assert.True(await store.ReviewAsync(record.Id, record.Version, record.Fingerprint,
            new(VerificationState.Approved, "Document reviewed"), Guid.NewGuid(), default));
        await using (var edit = fixture.Context())
        {
            var source = (await new CorSubmissionRepository(edit).GetCorSubmissionAsync(cor.Id))!;
            source.UpdateSubmission(null, null, CorVerificationStatus.Pending);
            await new CorSubmissionRepository(edit).UpdateCorSubmissionAsync(source);
        }
        Assert.Equal(CorVerificationStatus.Verified, (await db.CorSubmissions.AsNoTracking().SingleAsync()).VerificationStatus);
        Assert.Single(await store.ListAsync(null, true, default));
    }

    [PostgresVerificationFact]
    public async Task DuplicateStudentIdsCannotRaceOrOverwriteAnotherProfile()
    {
        await using var fixture = await DatabaseFixture.Create();
        var (firstUser, _) = await fixture.Seed();
        await using var setup = fixture.Context();
        var secondUser = new UserAccount("hash", "phone");
        var firstProfile = new UserProfile(firstUser.Id, "First", "Student", null, null);
        var secondProfile = new UserProfile(secondUser.Id, "Second", "Student", null, null);
        setup.Add(secondUser); setup.Add(firstProfile); setup.Add(secondProfile); await setup.SaveChangesAsync();
        async Task<bool> Add(Guid profileId, string number)
        {
            await using var db = fixture.Context();
            try { await new StudentRepository(db).AddAsync(new Student(profileId, number, "BSCS", "1A", 1)); return true; }
            catch (ParkFlow.Application.Common.RegistrationIdConflictException) { return false; }
        }
        var attempts = await Task.WhenAll(Add(firstProfile.Id, "2023-106763"), Add(secondProfile.Id, "2023106763"));
        Assert.Single(attempts, x => x);
        var winner = await setup.Students.AsNoTracking().SingleAsync();
        var otherId = winner.UserProfileId == firstProfile.Id ? secondProfile.Id : firstProfile.Id;
        Assert.True(await Add(otherId, "2023000000"));
        await using var edit = fixture.Context();
        var student = (await new StudentRepository(edit).GetByUserProfileIdAsync(otherId))!;
        student.UpdateDetails(winner.StudentNumber, "BSCS", "1A", 1);
        await Assert.ThrowsAsync<ParkFlow.Application.Common.RegistrationIdConflictException>(() => new StudentRepository(edit).UpdateAsync(student));
        Assert.Equal("2023000000", (await setup.Students.AsNoTracking().SingleAsync(s => s.UserProfileId == otherId)).StudentNumber);
    }

    [PostgresVerificationFact]
    public async Task StudentProfileEditsRetainVehicleBasedApprovalWithoutAccountReviewJobs()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, _) = await fixture.Seed(driver: true);
        await using var db = fixture.Context();
        var profile = await db.UserProfiles.SingleAsync(); profile.ReviewLicense(true, Guid.NewGuid(), "Checked");
        var student = new Student(profile.Id, "2023106763", "BSCS", "1A", 1);
        var identity = AuthIdentity.CreateManual(user.Id, "student@example.test", "hash", true); identity.MarkVerified();
        var vehicle = new Vehicle(user.Id,"IDN1234","Honda","hash",VehicleType.Car,"uploads/orcr.pdf","uploads/car.jpg",CorVerificationStatus.Verified);
        db.AddRange(student,identity,vehicle); await db.SaveChangesAsync(); var store = new VerificationStore(db);
        Assert.Null(await store.EnqueueAsync(VerificationType.Account,user.Id,default));
        Assert.Equal(AccountStatus.Active,(await db.UserAccounts.AsNoTracking().SingleAsync()).Status);
        await using (var edit = fixture.Context())
        {
            var source = (await new UserProfileRepository(edit).GetByUserIdAsync(user.Id))!;
            source.UpdateProfile(null,null,null,"uploads/replacement-profile.png"); await new UserProfileRepository(edit).UpdateAsync(source);
        }
        await using (var edit = fixture.Context())
        {
            var source = (await new StudentRepository(edit).GetByUserProfileIdAsync(profile.Id))!;
            source.UpdateDetails(source.StudentNumber,"BSIT",source.Section,source.YearLevel); await new StudentRepository(edit).UpdateAsync(source);
        }
        Assert.Equal(AccountStatus.Active,(await db.UserAccounts.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("BSIT",(await db.Students.AsNoTracking().SingleAsync()).Course);
        Assert.Empty(await store.ListAsync(user.Id,true,default));
        Assert.Empty(await db.VerificationRecords.ToListAsync());
    }

    [PostgresVerificationFact]
    public async Task MigrationScriptPreservesLegacyDataAndCanBeAppliedTwice()
    {
        await using var fixture = await DatabaseFixture.Create();
        var (user, cor) = await fixture.Seed();
        await using var db = fixture.Context();
        await db.Database.ExecuteSqlRawAsync("""
            DROP TABLE "VerificationRecords", "VerificationPolicies", "VerificationAudits";
            DROP INDEX "IX_Vehicles_NormalizedPlate";
            ALTER TABLE "CorSubmissions" DROP COLUMN "ScheduleVerificationStatus";
            CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
            """);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "automated-verification-migration.sql"));
        await db.Database.ExecuteSqlRawAsync(script);
        await db.Database.ExecuteSqlRawAsync(script);
        Assert.Equal(AccountStatus.Active, (await db.UserAccounts.SingleAsync(x => x.Id == user.Id)).Status);
        var preserved = await db.CorSubmissions.SingleAsync(x => x.Id == cor.Id);
        Assert.Equal(cor.CorDocumentUrl, preserved.CorDocumentUrl);
        Assert.Null(preserved.ScheduleVerificationStatus);
        var policy = await db.VerificationPolicies.SingleAsync();
        foreach (var type in Enum.GetValues<VerificationType>()) Assert.False(policy.Allows(type));
        Assert.Empty(await db.VerificationRecords.ToListAsync());
    }

    [PostgresVerificationFact]
    public async Task ConcurrentEnqueuesAndClaimsProduceOneCurrentRevisionAndOneLease()
    {
        await using var fixture = await DatabaseFixture.Create();
        var (user, cor) = await fixture.Seed();
        var records = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context(); return await new VerificationStore(db).EnqueueAsync(VerificationType.Cor, cor.Id, default);
        }));
        Assert.Single(records.Select(x => x!.Id).Distinct());
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Context(); return await new VerificationStore(db).ClaimAsync(TimeSpan.FromMinutes(3), default);
        }));
        Assert.Single(claims, x => x != null);
        await using var verify = fixture.Context();
        Assert.Equal(1, await verify.VerificationRecords.CountAsync(x => x.IsCurrent));
    }

    [PostgresVerificationFact]
    public async Task ResubmissionPreservesHistoryAndDiscardsOldWorkerDecision()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, cor) = await fixture.Seed(personnel: true);
        await using var first = fixture.Context(); var store = new VerificationStore(first);
        var original = await store.EnqueueAsync(VerificationType.Cor, cor.Id, default);
        var claim = (await store.ClaimAsync(TimeSpan.FromMinutes(3), default))!;
        await using (var edit = fixture.Context())
        {
            var source = (await edit.CorSubmissions.FindAsync(cor.Id))!;
            source.UpdateSubmission(null, "uploads/parkflow/cor/resubmission.pdf", CorVerificationStatus.Pending);
            await new CorSubmissionRepository(edit).UpdateCorSubmissionAsync(source);
        }
        Assert.False(await store.CompleteAsync(claim, new(VerificationState.Approved, "Old OCR result", TrustedEvidenceSucceeded: true) { PolicyVersion = 0 }, default));
        var history = await store.ListAsync(user.Id, true, default);
        Assert.Equal(2, history.Count); Assert.Single(history, x => x.IsCurrent);
        Assert.False(history.Single(x => x.Id == original!.Id).IsCurrent);
        Assert.Equal(AccountStatus.PendingVerification, (await first.UserAccounts.AsNoTracking().SingleAsync()).Status);
    }

    [PostgresVerificationFact]
    public async Task ManualDecisionWinsAgainstInFlightWorkerAndStaleReviewer()
    {
        await using var fixture = await DatabaseFixture.Create(); var (_, cor) = await fixture.Seed(personnel: true);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        await store.EnqueueAsync(VerificationType.Cor, cor.Id, default);
        var claim = (await store.ClaimAsync(TimeSpan.FromMinutes(3), default))!;
        var version = claim.Version; var lease = claim.LeaseId;
        Assert.True(await store.ReviewAsync(claim.Id, version, claim.Fingerprint, new(VerificationState.Approved, "Authenticity checked manually"), Guid.NewGuid(), default));
        Assert.False(await store.ReviewAsync(claim.Id, version, claim.Fingerprint, new(VerificationState.Rejected, "Stale decision"), Guid.NewGuid(), default));
        Assert.False(await store.CompleteAsync(claim, new(VerificationState.NeedsReview, "Old worker result") { PolicyVersion = 0 }, default));
        var result = (await store.FindAsync(claim.Id, default))!;
        Assert.Equal(VerificationState.Approved, result.Status); Assert.Equal(VerificationMethod.Manual, result.Method);
        Assert.NotNull(result.ReviewerId); Assert.Null(result.LeaseId); Assert.Equal(lease, claim.LeaseId);
        Assert.Contains(await store.AuditAsync(claim.Id, default), x => x.Action == "ManualReview");
    }

    [PostgresVerificationFact]
    public async Task RetriesStopAtLimitAndExpiredLeasesAreRecovered()
    {
        await using var fixture = await DatabaseFixture.Create(); var (_, cor) = await fixture.Seed();
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        await store.EnqueueAsync(VerificationType.Cor, cor.Id, default);
        var first = (await store.ClaimAsync(TimeSpan.FromMinutes(1), default))!;
        var oldLease = first.LeaseId;
        await db.VerificationRecords.Where(x => x.Id == first.Id).ExecuteUpdateAsync(x => x.SetProperty(r => r.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        var recovered = (await store.ClaimAsync(TimeSpan.FromMinutes(1), default))!;
        Assert.NotEqual(oldLease, recovered.LeaseId); Assert.Equal(2, recovered.Attempts);
        Assert.False(await store.RetryAsync(first, 2, default));
        Assert.True(await store.RetryAsync(recovered, 2, default));
        Assert.Equal(VerificationState.NeedsReview, (await store.FindAsync(first.Id, default))!.Status);
        Assert.Null(await store.ClaimAsync(TimeSpan.FromMinutes(1), default));
    }

    [PostgresVerificationFact]
    public async Task PolicyChangesAreAuditedAndCannotBypassTrustedChecks()
    {
        await using var fixture = await DatabaseFixture.Create(); var (_, cor) = await fixture.Seed(personnel: true);
        await using var db = fixture.Context(); var store = new VerificationStore(db);
        var actor = Guid.NewGuid(); var policy = await store.GetPolicyAsync(default);
        policy.CorAutomaticApproval = true;
        Assert.True(await store.UpdatePolicyAsync(policy, actor, default));
        Assert.False(await store.UpdatePolicyAsync(new VerificationPolicy { Version = 0 }, actor, default));
        Assert.Contains(await store.AuditAsync(null, default), x => x.ActorId == actor && x.Action == "PolicyChanged");
        await store.EnqueueAsync(VerificationType.Cor, cor.Id, default);
        var claim = (await store.ClaimAsync(TimeSpan.FromMinutes(1), default))!;
        Assert.True(await store.CompleteAsync(claim, new(VerificationState.Approved, "Matching text") { PolicyVersion = 1 }, default));
        Assert.Equal(VerificationState.PreVerified, (await store.FindAsync(claim.Id, default))!.Status);
    }

    [PostgresVerificationFact]
    public async Task DuplicateNormalizedPlatesAreRejectedByDatabaseDuringConcurrentRegistration()
    {
        await using var fixture = await DatabaseFixture.Create(); var (user, _) = await fixture.Seed();
        async Task<bool> Register(string plate)
        {
            await using var db = fixture.Context();
            try { await new VehicleRepository(db).AddAsync(new Vehicle(user.Id, plate, "Honda", Guid.NewGuid().ToString(), VehicleType.Motorcycle)); return true; }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { return false; }
            catch (ParkFlow.Application.Common.VehiclePlateConflictException) { return false; }
        }
        var results = await Task.WhenAll(Register("ABC-1234"), Register("abc 1234"));
        Assert.Single(results, x => x);
    }

    private sealed class DatabaseFixture(string connection, string schema) : IAsyncDisposable
    {
        public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, o => o.EnableRetryOnFailure()).Options);
        public static async Task<DatabaseFixture> Create()
        {
            var connection = Environment.GetEnvironmentVariable("PARKFLOW_VERIFICATION_TEST_CONNECTION");
            if (!PostgresVerificationFactAttribute.IsDisposableLocal(connection))
                throw new InvalidOperationException("Verification tests require a disposable local database ending _verification_test.");
            var builder = new NpgsqlConnectionStringBuilder(connection);
            var schema = "parkflow_verification_" + Guid.NewGuid().ToString("N"); builder.SearchPath = schema;
            var fixture = new DatabaseFixture(builder.ConnectionString, schema);
            await using var db = fixture.Context();
            // The identifier is generated exclusively from a fixed prefix and GUID.
            var createSchema = "CREATE SCHEMA \"" + schema + "\"";
            await db.Database.ExecuteSqlRawAsync(createSchema);
            // Execute generated DDL verbatim: EF defaults contain JSON braces, which
            // ExecuteSqlRaw's format-string processing would otherwise interpret.
            await db.Database.OpenConnectionAsync();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
            await using (var create = db.Database.GetDbConnection().CreateCommand())
            {
                create.Transaction = transaction.GetDbTransaction();
                create.CommandText = db.Database.GenerateCreateScript() + ParkFlow.Persistence.Migrations.EnforceVehiclePlateOwnership.PreflightSql + ParkFlow.Persistence.Migrations.EnforceVehiclePlateOwnership.AuthoritySql;
                await create.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            }
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_Vehicles_NormalizedPlate\" ON \"Vehicles\" (upper(replace(replace(\"PlateNumber\", '-', ''), ' ', '')))");
            return fixture;
        }
        public async Task<(UserAccount, CorSubmission)> Seed(bool personnel = false, bool driver = false)
        {
            await using var db = Context();
            var account = new UserAccount("hash", "phone"); account.Verify();
            db.Add(account); await db.SaveChangesAsync();
            var cor = new CorSubmission(account.Id, "2026-2027", "uploads/parkflow/cor/original.pdf", verificationStatus: CorVerificationStatus.Pending);
            db.Add(cor);
            if (personnel || driver)
            {
                var profile = new UserProfile(account.Id, "Juan", "Cruz", null, "uploads/profile.jpg");
                profile.SubmitLicense("uploads/license.jpg"); db.Add(profile);
                if (personnel) db.Add(new Personnel(profile.Id, "EMP-SYNTHETIC", "CICT", Roles.UniversityStaff));
            }
            await db.SaveChangesAsync(); return (account, cor);
        }
        public async ValueTask DisposeAsync()
        {
            await using (var db = Context())
            {
                if (!schema.StartsWith("parkflow_verification_", StringComparison.Ordinal) || schema.Length != 54) throw new InvalidOperationException("Unexpected test schema.");
                var dropSchema = "DROP SCHEMA \"" + schema + "\" CASCADE";
                await db.Database.ExecuteSqlRawAsync(dropSchema);
            }
            // Each generated search_path has its own pool. Dispose it with its schema
            // so the full suite cannot accumulate idle connections on PostgreSQL.
            using var poolKey = new NpgsqlConnection(connection);
            NpgsqlConnection.ClearPool(poolKey);
        }
    }
}
