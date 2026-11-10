using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Infrastructure.Verification;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;

namespace Test.Features.Auth;

/// <summary>
/// Smoke test against the deployed EC2 server. It registers a real account and sends a real OTP email,
/// and it fails whenever the server is down or mid-deploy, so it must not run on every CI build.
/// Run it deliberately with PARKFLOW_LIVE_EC2_TESTS=1.
/// </summary>
public sealed class LiveEc2FactAttribute : FactAttribute
{
    public const string EnableVariable = "PARKFLOW_LIVE_EC2_TESTS";

    public LiveEc2FactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
            Skip = $"Live EC2 smoke test. Set {EnableVariable}=1 to run it against the deployed server.";
    }
}

public class RegistrationCompleteTests
{
    private const string ConnectionString = "Host=parkflow.ci5g4wkmae5j.us-east-1.rds.amazonaws.com;Port=5432;Database=postgres;Username=parkflow;Password=ParkFlow2026!;SSL Mode=Require;Trust Server Certificate=true;Timeout=60;Command Timeout=60;Keepalive=30;";

    [Fact]
    public async Task Test_Student_SaveAsync_WithRealDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);

        var id = Guid.NewGuid();
        var studentNumber = "2023" + Random.Shared.Next(100000, 999999);
        var email = $"teststudent_{id:N}@example.test";
        var plateNumber = "TST" + Random.Shared.Next(1000, 9999);

        var details = new RegistrationDetails
        {
            Role = Roles.Student,
            Email = email,
            PhoneNumber = "09171234567",
            FirstName = "TestFirst",
            LastName = "TestLast",
            StudentNumber = studentNumber,
            Course = "BSIT",
            Section = "4A-G1",
            YearLevel = 4,
            AcademicYear = "2026-2027",
            Semester = "1st Semester",
            Brand = "Honda Click",
            PlateNumber = plateNumber,
            VehicleType = VehicleType.Motorcycle
        };

        var extractedFields = new CorFields(
            "TestLast, TestFirst",
            studentNumber,
            "BSIT",
            "2026-2027",
            "1st Semester",
            "4A-G1",
            "4"
        )
        {
            College = "College of Information and Communications Technology",
            Schedules =
            [
                new VerificationSchedule(DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0)),
                new VerificationSchedule(DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0))
            ]
        };

        var command = new CompleteRegistration(
            id,
            details,
            email,
            "https://res.cloudinary.com/synthetic/cor.pdf",
            "https://res.cloudinary.com/synthetic/orcr.jpg",
            "https://res.cloudinary.com/synthetic/motor.jpg",
            extractedFields,
            "private-uploads/parkflow/licenses/test.jpg",
            "https://res.cloudinary.com/synthetic/profile.jpg"
        );

        var writer = new RegistrationWriter(db);
        var result = await writer.SaveAsync(command, "hashed_password", "qr_hash", default);

        Assert.True(result.IsSuccess, $"SaveAsync failed with message: {result.Message}, errorCode: {result.ErrorCode}");
    }

    [Fact]
    public async Task Test_Cloudinary_Upload()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ParkFlow.Infrastructure.Cloudinary.CloudinarySettings
        {
            CloudName = "dp5p5kba8",
            ApiKey = "697415494571335",
            ApiSecret = "LJJemlAbkYeCnBCX3c6GOGghHJQ"
        });
        var cloud = new ParkFlow.Infrastructure.Cloudinary.CloudinaryService(options);
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII=");
        using var stream = new MemoryStream(bytes);
        var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, bytes.Length, "file", "test.png");

        var imgResult = await cloud.UploadImageAsync(formFile, "parkflow/motor-pictures");
        Assert.NotNull(imgResult.SecureUrl);

        var licenseResult = await cloud.UploadImageAsync(formFile, "parkflow/licenses");
        Assert.StartsWith("private-uploads/parkflow/licenses/", licenseResult.SecureUrl);
    }

    [Fact]
    public async Task Test_Cloudinary_UploadPdf()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ParkFlow.Infrastructure.Cloudinary.CloudinarySettings
        {
            CloudName = "dp5p5kba8",
            ApiKey = "697415494571335",
            ApiSecret = "LJJemlAbkYeCnBCX3c6GOGghHJQ"
        });
        var cloud = new ParkFlow.Infrastructure.Cloudinary.CloudinaryService(options);
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000052 00000 n \n0000000109 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n178\n%%EOF");
        using var stream = new MemoryStream(pdfBytes);
        var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, pdfBytes.Length, "cor", "cor.pdf");

        var result = await cloud.UploadPdfAsync(formFile, "parkflow/cor");
        Assert.NotNull(result.SecureUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.SecureUrl));
    }

    [Fact]
    public async Task Test_CorFile_FormFile_StreamLifecycle()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ParkFlow.Infrastructure.Cloudinary.CloudinarySettings
        {
            CloudName = "dp5p5kba8",
            ApiKey = "697415494571335",
            ApiSecret = "LJJemlAbkYeCnBCX3c6GOGghHJQ"
        });
        var cloud = new ParkFlow.Infrastructure.Cloudinary.CloudinaryService(options);
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII=");
        
        using var corStream = new MemoryStream(bytes, writable: false);
        var corFile = new Microsoft.AspNetCore.Http.FormFile(corStream, 0, corStream.Length, "cor", "test.png");

        // 1. Read in validate
        using (var s1 = corFile.OpenReadStream())
        {
            var buf = new byte[s1.Length];
            await s1.ReadExactlyAsync(buf);
            Assert.Equal(bytes.Length, buf.Length);
        }

        // 2. Read in hash
        using (var s2 = corFile.OpenReadStream())
        {
            var hash = await System.Security.Cryptography.SHA256.HashDataAsync(s2);
            Assert.NotEmpty(hash);
        }

        // 3. Read in Cloudinary UploadImageAsync
        var uploaded = await cloud.UploadImageAsync(corFile, "parkflow/cor");
        Assert.NotNull(uploaded.SecureUrl);
    }

    [Fact]
    public async Task Test_CompleteRegistrationHandler()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "9fYYITXanRbP15mk1Cpr3YQATfnlMG81tGUhdhHGYxcfcjrWwVqXaUak1uydkGCQlHWR2mWmFVfqaNcfR2UYbw",
                ["Jwt:Issuer"] = "ParkFlow",
                ["Jwt:Audience"] = "ParkFlowUsers",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var id = Guid.NewGuid();
        var studentNumber = "2023" + Random.Shared.Next(100000, 999999);
        var email = $"handler_test_{id:N}@example.test";
        var plateNumber = "TST" + Random.Shared.Next(1000, 9999);

        var details = new RegistrationDetails
        {
            Role = Roles.Student,
            Email = email,
            Password = "Password123!",
            PhoneNumber = "09171234567",
            FirstName = "TestFirst",
            LastName = "TestLast",
            StudentNumber = studentNumber,
            Course = "BSIT",
            Section = "4A-G1",
            YearLevel = 4,
            AcademicYear = "2026-2027",
            Semester = "1st Semester",
            Brand = "Honda Click",
            PlateNumber = plateNumber,
            VehicleType = VehicleType.Motorcycle
        };

        var extractedFields = new CorFields(
            "TestLast, TestFirst",
            studentNumber,
            "BSIT",
            "2026-2027",
            "1st Semester",
            "4A-G1",
            "4"
        )
        {
            College = "College of Information and Communications Technology",
            Schedules =
            [
                new VerificationSchedule(DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0)),
                new VerificationSchedule(DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0))
            ]
        };

        var command = new CompleteRegistration(
            id,
            details,
            email,
            "https://res.cloudinary.com/synthetic/cor.pdf",
            "https://res.cloudinary.com/synthetic/orcr.jpg",
            "https://res.cloudinary.com/synthetic/motor.jpg",
            extractedFields,
            "private-uploads/parkflow/licenses/test.jpg",
            "https://res.cloudinary.com/synthetic/profile.jpg"
        );

        var writer = new RegistrationWriter(db);
        var passwords = new PasswordHasherService();
        var jwt = new ParkFlow.Infrastructure.Security.JwtService(config);
        var qr = new ParkFlow.Infrastructure.QrCode.QrCodeService();
        var signalR = new FakeSignalR();
        var cache = new ParkFlow.Infrastructure.Caching.MemoryCacheService(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        var handler = new CompleteRegistrationHandler(writer, passwords, jwt, qr, signalR, cache);
        var result = await handler.Handle(command, default);

        Assert.True(result.IsSuccess, $"Handle failed: {result.Message}, errorCode: {result.ErrorCode}");
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Test_Inspect_Rds_Recent_Records()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);

        var recentOtps = await db.EmailOtps.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new { o.Email, o.OtpCode, o.ExpiresAt, o.IsUsed, o.CreatedAt })
            .ToListAsync();

        var recentUsers = await db.UserAccounts.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .Select(u => new { u.Id, u.PhoneNumber, u.Status, u.OnboardingStep, u.CreatedAt })
            .ToListAsync();

        var recentVehicles = await db.Vehicles.AsNoTracking()
            .OrderByDescending(v => v.CreatedAt)
            .Take(10)
            .Select(v => new { v.Id, v.PlateNumber, v.Brand, v.VehicleType, v.OwnerId, v.CreatedAt })
            .ToListAsync();
        Assert.NotEmpty(recentUsers);
    }

    [LiveEc2Fact]
    public async Task Test_EC2_EBike_Registration_Succeeds()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new AppDbContext(options);

        var testEmail = $"dean.test_{Guid.NewGuid():N}@gmail.com";
        using var client = new HttpClient { BaseAddress = new Uri("http://54.90.173.98:5000"), Timeout = TimeSpan.FromSeconds(60) };

        var sendOtpRes = await client.PostAsJsonAsync("/api/auth/send-email-otp", new { Email = testEmail });
        Assert.True(sendOtpRes.IsSuccessStatusCode);

        await Task.Delay(1000);
        var otpRecord = await db.EmailOtps.AsNoTracking()
            .Where(o => o.Email == testEmail)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(otpRecord);

        var verifyRes = await client.PostAsJsonAsync("/api/auth/verify-email-otp", new { Email = testEmail, OtpCode = otpRecord.OtpCode, Purpose = "Verification" });
        Assert.True(verifyRes.IsSuccessStatusCode);

        using var verifyDoc = JsonDocument.Parse(await verifyRes.Content.ReadAsStringAsync());
        var verificationToken = verifyDoc.RootElement.GetProperty("emailVerificationToken").GetString();
        Assert.NotNull(verificationToken);

        var sessionRes = await client.PostAsJsonAsync("/api/registration/session", new { Email = testEmail, EmailVerificationToken = verificationToken });
        Assert.True(sessionRes.IsSuccessStatusCode);

        using var sessionDoc = JsonDocument.Parse(await sessionRes.Content.ReadAsStringAsync());
        var sessionToken = sessionDoc.RootElement.GetProperty("data").GetProperty("token").GetString();
        Assert.NotNull(sessionToken);

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000052 00000 n \n0000000109 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n178\n%%EOF");
        using var corContent = new MultipartFormDataContent();
        corContent.Add(new ByteArrayContent(pdfBytes) { Headers = { { "Content-Type", "application/pdf" } } }, "file", "cor.pdf");
        client.DefaultRequestHeaders.Add("X-Registration-Token", sessionToken);

        await client.PostAsync("/api/registration/cor", corContent);

        var idCardNumber = "EMP" + Random.Shared.Next(100000, 999999);
        var detailsObj = new
        {
            email = testEmail,
            password = "Password123!",
            role = 2, // UniversityStaff
            firstName = "Dean Allen",
            lastName = "Esguerra",
            middleName = (string?)null,
            phoneNumber = "09171234567",
            idCardNumber = idCardNumber,
            department = "College of Information and Communications Technology",
            brand = "Fiido",
            plateNumber = (string?)null,
            vehicleType = 1 // ElectricBike
        };

        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII=");
        using var completeContent = new MultipartFormDataContent();
        completeContent.Add(new StringContent(JsonSerializer.Serialize(detailsObj)), "details");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "cor", "employee_id.png");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "motor", "ebike.png");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "profilePhoto", "profilePhoto.png");

        var completeRes = await client.PostAsync("/api/registration/complete", completeContent);
        var completeBody = await completeRes.Content.ReadAsStringAsync();

        Assert.True(completeRes.IsSuccessStatusCode, $"Expected success for EBike without license on EC2, but got: {completeRes.StatusCode} {completeBody}");
    }

    [LiveEc2Fact]
    public async Task Test_EC2_Motorcycle_With_License_Succeeds()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new AppDbContext(options);

        var testEmail = $"dean.test_{Guid.NewGuid():N}@gmail.com";
        using var client = new HttpClient { BaseAddress = new Uri("http://54.90.173.98:5000"), Timeout = TimeSpan.FromSeconds(60) };

        var sendOtpRes = await client.PostAsJsonAsync("/api/auth/send-email-otp", new { Email = testEmail });
        Assert.True(sendOtpRes.IsSuccessStatusCode);

        await Task.Delay(1000);
        var otpRecord = await db.EmailOtps.AsNoTracking()
            .Where(o => o.Email == testEmail)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(otpRecord);

        var verifyRes = await client.PostAsJsonAsync("/api/auth/verify-email-otp", new { Email = testEmail, OtpCode = otpRecord.OtpCode, Purpose = "Verification" });
        Assert.True(verifyRes.IsSuccessStatusCode);

        using var verifyDoc = JsonDocument.Parse(await verifyRes.Content.ReadAsStringAsync());
        var verificationToken = verifyDoc.RootElement.GetProperty("emailVerificationToken").GetString();
        Assert.NotNull(verificationToken);

        var sessionRes = await client.PostAsJsonAsync("/api/registration/session", new { Email = testEmail, EmailVerificationToken = verificationToken });
        Assert.True(sessionRes.IsSuccessStatusCode);

        using var sessionDoc = JsonDocument.Parse(await sessionRes.Content.ReadAsStringAsync());
        var sessionToken = sessionDoc.RootElement.GetProperty("data").GetProperty("token").GetString();
        Assert.NotNull(sessionToken);

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000052 00000 n \n0000000109 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n178\n%%EOF");
        using var corContent = new MultipartFormDataContent();
        corContent.Add(new ByteArrayContent(pdfBytes) { Headers = { { "Content-Type", "application/pdf" } } }, "file", "cor.pdf");
        client.DefaultRequestHeaders.Add("X-Registration-Token", sessionToken);

        await client.PostAsync("/api/registration/cor", corContent);

        var idCardNumber = "EMP" + Random.Shared.Next(100000, 999999);
        var plateNumber = "TST" + Random.Shared.Next(1000, 9999);
        var detailsObj = new
        {
            email = testEmail,
            password = "Password123!",
            role = 2, // UniversityStaff
            firstName = "Dean Allen",
            lastName = "Esguerra",
            middleName = (string?)null,
            phoneNumber = "09171234567",
            idCardNumber = idCardNumber,
            department = "College of Information and Communications Technology",
            brand = "Honda Click",
            plateNumber = plateNumber,
            vehicleType = 0 // Motorcycle
        };

        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII=");
        using var completeContent = new MultipartFormDataContent();
        completeContent.Add(new StringContent(JsonSerializer.Serialize(detailsObj)), "details");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "cor", "employee_id.png");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "orcr", "orcr.png");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "motor", "motor.png");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "license", "license.png");
        completeContent.Add(new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "profilePhoto", "profilePhoto.png");

        var completeRes = await client.PostAsync("/api/registration/complete", completeContent);
        var completeBody = await completeRes.Content.ReadAsStringAsync();

        Assert.True(completeRes.IsSuccessStatusCode, $"Expected success for Motorcycle with license on EC2, but got: {completeRes.StatusCode} {completeBody}");
    }

    [Fact]
    public async Task Test_RegistrationDraftService_CompleteAsync_FullFlow()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "9fYYITXanRbP15mk1Cpr3YQATfnlMG81tGUhdhHGYxcfcjrWwVqXaUak1uydkGCQlHWR2mWmFVfqaNcfR2UYbw",
                ["Jwt:Issuer"] = "ParkFlow",
                ["Jwt:Audience"] = "ParkFlowUsers",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(ConnectionString));
        services.AddScoped<IRegistrationWriter, RegistrationWriter>();
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<IJwtService, ParkFlow.Infrastructure.Security.JwtService>();
        services.AddScoped<IQrCodeService, ParkFlow.Infrastructure.QrCode.QrCodeService>();
        services.AddSingleton<ISignalRNotificationSender, FakeSignalR>();
        services.AddSingleton<ICacheService, ParkFlow.Infrastructure.Caching.MemoryCacheService>();
        services.AddMemoryCache();
        services.AddScoped<IEmailVerificationReceipt, ParkFlow.Infrastructure.Security.EmailVerificationReceipt>();

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new ParkFlow.Infrastructure.Cloudinary.CloudinarySettings
        {
            CloudName = "dp5p5kba8",
            ApiKey = "697415494571335",
            ApiSecret = "LJJemlAbkYeCnBCX3c6GOGghHJQ"
        }));
        services.AddScoped<ICloudinaryService, ParkFlow.Infrastructure.Cloudinary.CloudinaryService>();
        services.AddScoped<ICorDocumentReader, SyntheticReader>();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<CompleteRegistrationHandler>());

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RegistrationDraftService>.Instance;
        var draftService = new RegistrationDraftService(scopeFactory, logger);

        var id = Guid.NewGuid();
        var studentNumber = "2023999999"; // The existing student in RDS
        var email = $"draft_student_{id:N}@example.test";
        var plateNumber = "TST" + Random.Shared.Next(1000, 9999);

        var receipt = sp.GetRequiredService<IEmailVerificationReceipt>();
        var verificationToken = receipt.Issue(email);

        var session = await draftService.StartAsync(email, verificationToken, default);
        Assert.NotNull(session.Token);

        // Upload COR
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000052 00000 n \n0000000109 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n178\n%%EOF");
        using var corStream = new MemoryStream(pdfBytes);
        var corFile = new Microsoft.AspNetCore.Http.FormFile(corStream, 0, pdfBytes.Length, "file", "student_cor.pdf");
        var preview = await draftService.UploadCorAsync(session.Token, corFile, default);
        Assert.NotNull(preview);

        // Set Fields via reflection on the internal draft object
        var sessionsField = typeof(RegistrationDraftService).GetField("sessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var cache = (Microsoft.Extensions.Caching.Memory.IMemoryCache)sessionsField.GetValue(draftService)!;
        var keyMethod = typeof(RegistrationDraftService).GetMethod("Key", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var cacheKey = (string)keyMethod.Invoke(null, [session.Token])!;
        cache.TryGetValue(cacheKey, out var draft);
        Assert.NotNull(draft);

        var extractedFields = new CorFields(
            "TestLast, TestFirst",
            studentNumber,
            "BSIT",
            "2026-2027",
            "1st Semester",
            "4A-G1",
            "4"
        )
        {
            College = "College of Information and Communications Technology",
            Schedules =
            [
                new VerificationSchedule(DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0)),
                new VerificationSchedule(DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0))
            ]
        };

        var fieldsProp = draft.GetType().GetField("Fields", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        fieldsProp.SetValue(draft, extractedFields);

        var details = new RegistrationDetails
        {
            Role = Roles.Student,
            Email = email,
            Password = "Password123!",
            PhoneNumber = "09171234567",
            FirstName = "TestFirst",
            LastName = "TestLast",
            StudentNumber = studentNumber,
            Course = "BSIT",
            Section = "4A-G1",
            YearLevel = 4,
            AcademicYear = "2026-2027",
            Semester = "1st Semester",
            Brand = "Honda Click",
            PlateNumber = plateNumber,
            VehicleType = VehicleType.Motorcycle
        };

        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII=");
        using var orcrStream = new MemoryStream(pngBytes);
        var orcrFile = new Microsoft.AspNetCore.Http.FormFile(orcrStream, 0, pngBytes.Length, "orcr", "orcr.png");
        using var motorStream = new MemoryStream(pngBytes);
        var motorFile = new Microsoft.AspNetCore.Http.FormFile(motorStream, 0, pngBytes.Length, "motor", "motor.png");
        using var licenseStream = new MemoryStream(pngBytes);
        var licenseFile = new Microsoft.AspNetCore.Http.FormFile(licenseStream, 0, pngBytes.Length, "license", "license.png");
        using var profileStream = new MemoryStream(pngBytes);
        var profileFile = new Microsoft.AspNetCore.Http.FormFile(profileStream, 0, pngBytes.Length, "profilePhoto", "profile.png");

        var completeResult = await draftService.CompleteAsync(session.Token, details, null, orcrFile, motorFile, licenseFile, profileFile, default);
        Assert.False(completeResult.IsSuccess, $"CompleteAsync unexpectedly succeeded: {completeResult.Message}");
        Assert.Equal(ErrorCode.Conflict, completeResult.ErrorCode);
        Assert.Equal("This student or employee ID number is already registered.", completeResult.Message);
    }

    private sealed class SyntheticReader : ICorDocumentReader
    {
        public bool IsStoredDocument(string url) => true;
        public Task<DocumentText?> ReadTextLayerAsync(Stream stream, string fileName, CancellationToken ct) =>
            Task.FromResult<DocumentText?>(new DocumentText("Test text", "DigitalPdfTextExtracted"));
        public Task<DocumentText> ReadAsync(Stream stream, string fileName, CancellationToken ct) =>
            Task.FromResult(new DocumentText("Test text", "DigitalPdfTextExtracted"));
        public Task ValidateAsync(Stream stream, string fileName, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSignalR : ISignalRNotificationSender
    {
        public Task SendEventNotificationAsync(string userId, object data) => Task.CompletedTask;
        public Task SendToUserAsync(string userId, string method, object data) => Task.CompletedTask;
        public Task SendToAllAsync(string method, object data) => Task.CompletedTask;
    }
}




