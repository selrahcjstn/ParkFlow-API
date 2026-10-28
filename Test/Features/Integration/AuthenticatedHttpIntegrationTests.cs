using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ParkFlow.API.Controllers;
using ParkFlow.API.Security;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Infrastructure.Security;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Test.Features.Visitors;
using Test.Support;
using Xunit;

namespace Test.Features.Integration;

[Collection("Parking deadline settings")]
public class AuthenticatedHttpIntegrationTests(VisitorPostgresDatabase database) : IClassFixture<VisitorPostgresDatabase>
{
    [VisitorPostgresFact]
    public async Task VisitorLifecycleRequiresConfirmationAndKeepsIndependentVehicleSessions()
    {
        await using var host = await Host.Create(database);
        using var anonymous = host.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("api/visitors/lookup/HTTP123")).StatusCode);
        using var student = host.Client("student");
        Assert.Equal(HttpStatusCode.Forbidden,(await student.GetAsync("api/visitors/lookup/HTTP123")).StatusCode);
        using var guard = host.Client("guard");
        var plate = "HT" + Guid.NewGuid().ToString("N")[..8];
        using var initial = await guard.PostAsync("api/visitors/entry", Entry(plate));
        var first = await Success(initial); var id = first.GetProperty("visitorId").GetGuid();
        var vehicleId = first.GetProperty("visitorVehicleId").GetGuid(); var sessionId = first.GetProperty("sessionId").GetGuid();
        var preview = await Success(await guard.GetAsync("api/visitors/lookup/"+plate));
        Assert.Equal(JsonValueKind.Null,preview.GetProperty("fullName").ValueKind);
        Assert.Equal(JsonValueKind.Null,preview.GetProperty("driversLicenseUrl").ValueKind);
        var confirmed = await Success(await guard.PostAsync("api/visitors/lookup/"+plate+"/confirm",null));
        Assert.Equal("Synthetic Visitor",confirmed.GetProperty("fullName").GetString());
        Assert.Equal(id,confirmed.GetProperty("visitorId").GetGuid());
        var secondPlate = "HM" + Guid.NewGuid().ToString("N")[..8];
        Assert.Equal(HttpStatusCode.BadRequest,(await guard.PostAsync("api/visitors/entry",Entry(secondPlate,id,false))).StatusCode);
        var second = await Success(await guard.PostAsync("api/visitors/entry",Entry(secondPlate,id,true,VehicleType.Motorcycle)));
        Assert.Equal(id,second.GetProperty("visitorId").GetGuid());
        Assert.NotEqual(vehicleId,second.GetProperty("visitorVehicleId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict,(await guard.PostAsync("api/visitors/entry",Entry(plate))).StatusCode);
        var exit = await guard.PatchAsJsonAsync("api/visitors/exit",new { plateNumber=plate, guardUserId=Guid.NewGuid(), visitorVehicleId=vehicleId, sessionId });
        await Success(exit);
        await using (var db = database.Context())
        {
            var old = await db.VisitSessions.SingleAsync(s=>s.Id==sessionId);
            Assert.Equal(VisitSessionStatus.Completed,old.Status); Assert.NotNull(old.ExitTime);
            Assert.Equal(host.GuardProfileId,old.EntryGuardId); Assert.Equal(host.GuardProfileId,old.ExitGuardId);
            Assert.Equal(host.GuardId,old.LicenseVerifiedBy);
            Assert.Equal(1,await db.VisitSessions.CountAsync(s=>s.VisitorId==id&&s.Status==VisitSessionStatus.Inside));
        }
        var history = await Success(await guard.GetAsync($"api/visitors/{id}?visitorVehicleId={vehicleId}"));
        Assert.Equal(2,history.GetProperty("visitor").GetProperty("vehicles").GetArrayLength());
        Assert.Single(history.GetProperty("visits").EnumerateArray());
        var returned = await Success(await guard.PostAsync("api/visitors/entry",Entry(plate)));
        Assert.True(returned.GetProperty("isReturning").GetBoolean()); Assert.Equal(id,returned.GetProperty("visitorId").GetGuid());
        Assert.NotEqual(sessionId,returned.GetProperty("sessionId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict,(await guard.PatchAsJsonAsync("api/visitors/exit",new {plateNumber=plate,visitorVehicleId=vehicleId,sessionId})).StatusCode);
    }

    [VisitorPostgresFact]
    public async Task HistoryAndOwnedDeadlineRequireAuthenticatedAuthorizedCallers()
    {
        await using var host = await Host.Create(database);
        using var anonymous = host.Client(); using var owner = host.Client("student"); using var guard = host.Client("guard");
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("api/parking-logs/history/page/1/10")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("api/parking-logs/history/page/1/10")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"api/parking-logs/active/vehicle/{host.VehicleId}")).StatusCode);
        var details = await Success(await owner.GetAsync($"api/parking-logs/active/vehicle/{host.VehicleId}"));
        Assert.Equal(new DateTime(2026,10,28,2,15,0,DateTimeKind.Utc),details.GetProperty("exitBy").GetDateTime());
        await Success(await guard.GetAsync($"api/parking-logs/active/vehicle/{host.VehicleId}"));
        using var other = host.Client("student",Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"api/parking-logs/active/vehicle/{host.VehicleId}")).StatusCode);
        using var passwordless = host.Client("guard",method:"microsoft");
        Assert.Equal(HttpStatusCode.Forbidden,(await passwordless.PostAsync("api/visitors/lookup/HTTP123/confirm",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("api/parking-logs/entry",new {qrCodeHash="synthetic",userId=Guid.NewGuid()})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.PatchAsJsonAsync("api/parking-logs/exit",new {qrCodeHash="synthetic",userId=host.GuardId})).StatusCode);
        using var invalid = host.Client(); invalid.DefaultRequestHeaders.Authorization = new("Bearer","invalid.synthetic.token");
        Assert.Equal(HttpStatusCode.Unauthorized,(await invalid.GetAsync($"api/parking-logs/active/vehicle/{host.VehicleId}")).StatusCode);
    }

    [VisitorPostgresFact]
    public async Task ProductionTransportAndPrivateDocumentAuthorizationAreEnforced()
    {
        await using var host = await Host.Create(database);
        using var cleartext = host.Client(secureProxy:false);
        Assert.Equal(HttpStatusCode.BadRequest,(await cleartext.GetAsync("api/visitors/lookup/HTTP123")).StatusCode);
        using var anonymous = host.Client(); using var owner = host.Client("student"); using var other = host.Client("student",Guid.NewGuid());
        var path = "private-uploads/parkflow/licenses/"+host.OwnerId.ToString("N")+".png";
        var endpoint = "api/files/document/url?url="+Uri.EscapeDataString(path);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await other.GetAsync(endpoint)).StatusCode);
        var signedResponse = await owner.GetAsync(endpoint); signedResponse.EnsureSuccessStatusCode();
        var signed = (await signedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        Assert.StartsWith("/api/files/document?",signed);
        using var document = await anonymous.GetAsync(signed); document.EnsureSuccessStatusCode();
        Assert.Contains("no-store",document.Headers.CacheControl!.ToString());
        var tampered = signed.Replace(host.OwnerId.ToString("N"),Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(tampered)).StatusCode);
    }

    [VisitorPostgresFact]
    public async Task AdminReactivationRecalculatesVehicleApprovalInsteadOfBypassingIt()
    {
        await using var host = await Host.Create(database);
        using var student = host.Client("student"); using var admin = host.Client("admin");
        var route=$"api/users/{host.OwnerId}/status";
        Assert.Equal(HttpStatusCode.Forbidden,(await student.PutAsJsonAsync(route,new {status="Suspended"})).StatusCode);
        await Success(await admin.PutAsJsonAsync(route,new {status="Suspended"}));
        var restored=await Success(await admin.PutAsJsonAsync(route,new {status="Active"}));
        Assert.Equal("Active",restored.GetProperty("status").GetString());
        await Success(await admin.PutAsJsonAsync(route,new {status="Suspended"}));
        await using (var db=database.Context()) { var vehicle=await db.Vehicles.SingleAsync(v=>v.Id==host.VehicleId); vehicle.UpdateVerificationStatus(CorVerificationStatus.Pending); await db.SaveChangesAsync(); }
        restored=await Success(await admin.PutAsJsonAsync(route,new {status="Active"}));
        Assert.Equal("PendingVerification",restored.GetProperty("status").GetString());
        await using var check=database.Context(); Assert.Equal(AccountStatus.PendingVerification,(await check.UserAccounts.SingleAsync(u=>u.Id==host.OwnerId)).Status);
    }

    [VisitorPostgresFact]
    public async Task GuardQrExitCompletesReservationAndReentryUsesApprovedDocumentsWithoutFinancialWrites()
    {
        await using var host=await Host.Create(database,liveParking:true); using var guard=host.Client("guard");
        var financialBefore=await FinancialCounts(database);
        var exit=await Success(await guard.PatchAsJsonAsync("api/parking-logs/exit",new {qrCodeHash=host.QrHash,userId=Guid.NewGuid()}));
        await using (var db=database.Context())
        {
            var log=await db.ParkingLogs.SingleAsync(p=>p.VehicleId==host.VehicleId); Assert.Equal(ParkingStatus.Exited,log.Status); Assert.NotNull(log.ExitTime); Assert.Equal(host.GuardProfileId,log.GuardId);
            Assert.Equal(ReservationStatus.Completed,(await db.ParkingReservations.SingleAsync(r=>r.UserId==host.OwnerId)).Status);
        }
        await Success(await guard.PostAsJsonAsync("api/parking-logs/entry",new {qrCodeHash=host.QrHash,userId=Guid.NewGuid()}));
        await using (var db=database.Context()) Assert.Equal(1,await db.ParkingLogs.CountAsync(p=>p.VehicleId==host.VehicleId&&p.Status==ParkingStatus.Parked&&p.ExitTime==null));
        Assert.Equal(financialBefore,await FinancialCounts(database));
    }
    private static async Task<long> FinancialCounts(VisitorPostgresDatabase database)
    {
        await using var db=database.Context();await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText="SELECT (SELECT count(*) FROM \"Violations\")+(SELECT count(*) FROM \"Feedbacks\" WHERE \"InvoiceNumber\" IS NOT NULL)";return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<JsonElement> Success(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,$"HTTP {(int)response.StatusCode}: {content}");
        using var doc = JsonDocument.Parse(content); return doc.RootElement.GetProperty("data").Clone();
    }
    private static long nextStudentNumber = 2026000000;
    private static MultipartFormDataContent Entry(string plate,Guid? visitor=null,bool confirmed=false,VehicleType type=VehicleType.Car)
    {
        var form = new MultipartFormDataContent();
        foreach(var pair in new Dictionary<string,string> { ["PlateNumber"]=plate,["FullName"]="Synthetic Visitor",["ContactNumber"]="09170000000",["Brand"]="Synthetic",["VehicleType"]=((int)type).ToString(),["LicenseVerified"]="true",["IdentityConfirmed"]=confirmed.ToString(),["GuardUserId"]=Guid.NewGuid().ToString() }) form.Add(new StringContent(pair.Value),pair.Key);
        if(visitor.HasValue)form.Add(new StringContent(visitor.ToString()!),"ExistingVisitorId");
        foreach(var name in new[]{"License","ProfilePhoto","VehiclePhoto"})
        {
            var file=new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII="));
            file.Headers.ContentType=new("image/png");form.Add(file,name,"synthetic.png");
        }
        return form;
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly IConfiguration config;
        private readonly Uri address;
        public Guid GuardId {get;private set;}
        public Guid GuardProfileId {get;private set;}
        public Guid OwnerId {get;private set;}
        public Guid AdminId {get;private set;}
        public string QrHash {get;}="synthetic-"+Guid.NewGuid().ToString("N");
        private bool liveParking;
        public Guid VehicleId {get;private set;}
        private Host(WebApplication app,IConfiguration config,Uri address){this.app=app;this.config=config;this.address=address;}
        public static async Task<Host> Create(VisitorPostgresDatabase database,bool liveParking=false)
        {
            var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName=Environments.Production, ContentRootPath=Path.GetTempPath() });
            builder.Configuration.Sources.Clear();builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Release:PublicApiUrl"]="https://api.example.invalid/api",["Jwt:Key"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),["Jwt:Issuer"]="synthetic-issuer",["Jwt:Audience"]="synthetic-audience",["Jwt:ExpiryMinutes"]="10" });
            builder.Logging.ClearProviders();builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            using var context=database.Context();var connection=context.Database.GetConnectionString();
            builder.Services.AddDbContext<AppDbContext>(o=>o.UseNpgsql(connection));
            builder.Services.AddScoped<IVisitorRepository,VisitorRepository>();builder.Services.AddScoped<IVehicleRepository,VehicleRepository>();
            builder.Services.AddScoped<IUserProfileRepository,UserProfileRepository>();builder.Services.AddScoped<IGuardRepository,GuardRepository>();
            builder.Services.AddScoped<IAdminRepository,AdminRepository>();builder.Services.AddScoped<IStudentRepository,StudentRepository>();
            builder.Services.AddScoped<IPersonnelRepository,PersonnelRepository>();builder.Services.AddScoped<IParkingLogRepository,ParkingLogRepository>();
            builder.Services.AddScoped<ICorSubmissionRepository,CorSubmissionRepository>();builder.Services.AddScoped<IParkingScheduleRepository,ParkingScheduleRepository>();
            builder.Services.AddScoped<IParkingReservationRepository,ParkingReservationRepository>();
            builder.Services.AddScoped<IUserAccountRepository,UserAccountRepository>();builder.Services.AddScoped<IVerificationStore,VerificationStore>();
            builder.Services.AddScoped<IParkingService,ParkingService>();builder.Services.AddScoped<IScheduleService,ScheduleService>();
            builder.Services.AddScoped<IParkingLogRoleService,ParkingLogRoleService>();builder.Services.AddSingleton<ISignalRNotificationSender,NoRealtime>();
            builder.Services.AddScoped<FluentValidation.IValidator<ExitParkingLogCommand>,ExitParkingLogValidator>();
            builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<IUserContext,UserContext>();
            builder.Services.AddSingleton<ICloudinaryService,SyntheticStorage>();
            builder.Services.AddSingleton<TimeProvider>(liveParking?TimeProvider.System:new Clock());
            builder.Services.AddMediatR(c=>c.RegisterServicesFromAssemblyContaining<ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup.GetPlateLookupHandler>());
            builder.Services.AddControllers().AddApplicationPart(typeof(VisitorController).Assembly);
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();builder.Services.AddSingleton<PrivateDocumentLinks>();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options=> {
                options.TokenValidationParameters=new TokenValidationParameters { ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,ValidateIssuerSigningKey=true,ValidIssuer=builder.Configuration["Jwt:Issuer"],ValidAudience=builder.Configuration["Jwt:Audience"],IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)) };
            });
            builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
            builder.Services.Configure<ForwardedHeadersOptions>(o=>ReleaseConfiguration.ConfigureProxy(o,builder.Configuration));
            ReleaseConfiguration.Validate(builder.Configuration,builder.Environment);
            var app=builder.Build();app.UseReleaseTransport(false);app.UseAuthentication();app.UseAuthorization();app.MapControllers();
            await app.StartAsync();
            var address=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            var host=new Host(app,builder.Configuration,address) { liveParking=liveParking };
            try { await host.Seed(); return host; } catch { await host.DisposeAsync(); throw; }
        }
        private async Task Seed()
        {
            await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var guard=new UserAccount("synthetic","09170000000");guard.Verify();GuardId=guard.Id;
            var gp=new UserProfile(guard.Id,"Synthetic","Guard",null,null);GuardProfileId=gp.Id;
            var admin=new UserAccount("synthetic","09170000002");admin.Verify();AdminId=admin.Id;var adminProfile=new UserProfile(admin.Id,"Synthetic","Admin",null,null);
            db.AddRange(admin,adminProfile,new Admin(adminProfile,RoleLevel.Admin));
            var owner=new UserAccount("synthetic","09170000001");owner.Verify();owner.UpdateOnboardingStep(OnboardingStep.Done);OwnerId=owner.Id;
            var identity=AuthIdentity.CreateManual(owner.Id,owner.Id+"@example.invalid","synthetic",true);identity.MarkVerified();db.Add(identity);
            var profile=new UserProfile(owner.Id,"Synthetic","Student",null,"uploads/synthetic.png");
            profile.SubmitLicense("private-uploads/parkflow/licenses/"+owner.Id.ToString("N")+".png");profile.ReviewLicense(true,guard.Id,"Synthetic check");
            var vehicle=new Vehicle(owner.Id,"HO"+Guid.NewGuid().ToString("N")[..8],"Synthetic",QrHash,VehicleType.Car,"uploads/synthetic.pdf","uploads/car.png",CorVerificationStatus.Verified);VehicleId=vehicle.Id;
            var cor=new CorSubmission(owner.Id,"2026-2027","uploads/cor.pdf",verificationStatus:CorVerificationStatus.Verified);cor.UpdateScheduleVerification(CorVerificationStatus.Verified);
            var log=new ParkingLog(vehicle.Id,gp.Id,ParkingStatus.Parked);
            db.AddRange(guard,gp,new Guard(gp,1),owner,profile,new Student(profile.Id,Interlocked.Increment(ref nextStudentNumber).ToString(),"BSCS","4E-G1",4),vehicle,cor,
                new ParkingSchedule(cor.Id,liveParking?ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).DayOfWeek:DayOfWeek.Wednesday,liveParking?TimeSpan.Zero:TimeSpan.FromHours(8),liveParking?new TimeSpan(23,59,59):TimeSpan.FromHours(10)),log);
            db.Entry(log).Property(p=>p.EntryTime).CurrentValue=liveParking?DateTime.UtcNow:new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc);
            if(liveParking) { var reservation=new ParkingReservation(owner.Id,"SYN-"+Guid.NewGuid().ToString("N"),ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date,TimeSpan.Zero,new TimeSpan(23,59,59),"Synthetic reservation",vehicleId:vehicle.Id,reservedVehicleType:VehicleType.Car);reservation.Confirm();db.Add(reservation); }
            await db.SaveChangesAsync();
        }
        public HttpClient Client(string? role=null,Guid? user=null,string method="pwd",bool secureProxy=true)
        {
            var client=new HttpClient { BaseAddress=address, Timeout=TimeSpan.FromSeconds(30) };
            if(secureProxy)client.DefaultRequestHeaders.Add("X-Forwarded-Proto","https");
            if(role!=null){var account=new UserAccount("synthetic",null){Id=user??(role=="guard"?GuardId:role=="admin"?AdminId:OwnerId)};client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtService(config).GenerateToken(account,role,method));}
            return client;
        }
        public async ValueTask DisposeAsync(){await app.StopAsync();await app.DisposeAsync();}
    }
    private sealed class NoRealtime : ISignalRNotificationSender
    {
        public Task SendEventNotificationAsync(string userId,object data)=>Task.CompletedTask;
        public Task SendToUserAsync(string userId,string method,object data)=>Task.CompletedTask;
        public Task SendToAllAsync(string method,object data)=>Task.CompletedTask;
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,28,0,30,0,TimeSpan.Zero); }
    private sealed class SyntheticStorage : ICloudinaryService
    {
        public Task<(string SecureUrl,string PublicId)> UploadImageAsync(IFormFile file,string folder)=>Task.FromResult((folder=="parkflow/licenses"?"private-uploads/parkflow/licenses/"+Guid.NewGuid().ToString("N")+".png":"uploads/synthetic/"+Guid.NewGuid().ToString("N")+".png","synthetic"));
        public Task<(string SecureUrl,string PublicId)> UploadPdfAsync(IFormFile file,string folder)=>UploadImageAsync(file,folder);
        public Task DeleteFileAsync(string publicId,bool isImage=true)=>Task.CompletedTask;
        public string? GetAuthenticatedDownloadUrl(string url,bool attachment=false)=>null;
        public Task<(Stream Stream,string ContentType,string FileName)?> GetDocumentStreamAsync(string url,bool attachment=false)=>Task.FromResult<(Stream,string,string)?>((new MemoryStream([1,2,3]),"image/png","synthetic.png"));
    }
}
