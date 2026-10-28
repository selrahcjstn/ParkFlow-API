using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using Test.Features.Auth;
using Test.Features.Cor;
using Xunit;

namespace Test.Features.Deployment;

public class BootstrapSecretTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public async Task MissingOrWeakSecretStopsBeforeAccountWrites(string? secret)
    {
        using var fixture = new Fixture(secret, null);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SuperAdminSeeder.SeedSuperAdminAsync(fixture.Services));
        Assert.Contains("Seeding:SuperAdmin:Password", error.Message);
        Assert.Empty(fixture.Users.Users);
        Assert.Empty(fixture.Hasher.Inputs);
    }
    [Fact]
    public async Task AllMissingAccountSecretsAreRequiredBeforeProvisioning()
    {
        using var fixture = new Fixture(Guid.NewGuid().ToString(), null);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SuperAdminSeeder.SeedSuperAdminAsync(fixture.Services));
        Assert.Contains("Seeding:Admin:Password", error.Message);
        Assert.Empty(fixture.Users.Users);
    }
    [Fact]
    public async Task InjectedSecretsInitializeBothRolesAndAreHashed()
    {
        using var fixture = new Fixture(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        await SuperAdminSeeder.SeedSuperAdminAsync(fixture.Services);
        Assert.Equal(2, fixture.Users.Users.Count);
        Assert.Equal(2, fixture.Hasher.Inputs.Count);
        Assert.Contains(fixture.Admins.Admins, a => a.RoleLevel == RoleLevel.SuperAdmin);
        Assert.Contains(fixture.Admins.Admins, a => a.RoleLevel == RoleLevel.Admin);
        Assert.All(fixture.Users.Users, u => Assert.StartsWith("hashed:", u.PasswordHash));
        Assert.All(fixture.Identities.Identities, i => Assert.True(i.IsVerified));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingAccountsAndLegacyRenameNeverResetPasswords(bool legacy)
    {
        using var fixture = new Fixture(null, null);
        var super = fixture.Existing(legacy ? "legacy-superadmin@example.invalid" : "superadmin@example.invalid", RoleLevel.SuperAdmin);
        var admin = fixture.Existing("admin@example.invalid", RoleLevel.Admin);
        var hashes = fixture.Identities.Identities.Select(i => i.PasswordHash).ToArray();
        await SuperAdminSeeder.SeedSuperAdminAsync(fixture.Services);
        Assert.Equal("existing-hash", super.PasswordHash);
        Assert.Equal("existing-hash", admin.PasswordHash);
        Assert.Equal(hashes, fixture.Identities.Identities.Select(i => i.PasswordHash));
        Assert.Empty(fixture.Hasher.Inputs);
        Assert.Contains(fixture.Identities.Identities, i => i.Email == "superadmin@example.invalid");
    }
    [Fact]
    public async Task MissingBootstrapEmailFailsWithoutProvisioningOrLoggingItsValue()
    {
        using var fixture = new Fixture(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        fixture.Services.GetRequiredService<IConfiguration>()["Seeding:SuperAdmin:Email"] = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SuperAdminSeeder.SeedSuperAdminAsync(fixture.Services));
        Assert.Contains("Seeding:SuperAdmin:Email", error.Message); Assert.Empty(fixture.Users.Users);
    }
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData(null)]
    public async Task SampleProvisioningRequiresAnExplicitDevelopmentHost(string? environment)
    {
        using var fixture = new Fixture(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), environment);
        fixture.Services.GetRequiredService<IConfiguration>()["Seeding:IncludeTestData"] = "true";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SuperAdminSeeder.SeedSuperAdminAsync(fixture.Services));
        Assert.Contains("Development", error.Message); Assert.Empty(fixture.Users.Users); Assert.Empty(fixture.Hasher.Inputs);
    }
    private sealed class Hasher : IPasswordHasher
    {
        public List<string> Inputs { get; } = [];
        public string HashPassword(string value) { Inputs.Add(value); return "hashed:" + Inputs.Count; }
        public bool VerifyPassword(string hash, string value) => false;
    }
    private sealed class Fixture : IDisposable
    {
        public Test.Features.Cor.FakeUserAccountRepository Users { get; } = new();
        public RegRoleAuthIdentityRepository Identities { get; } = new();
        public RegRoleUserProfileRepository Profiles { get; } = new();
        public RegRoleAdminRepository Admins { get; } = new();
        public Hasher Hasher { get; } = new();
        public ServiceProvider Services { get; }
        public Fixture(string? super, string? admin, string? environment = null)
        {
            var capture = new History();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .AddInterceptors(new NoConnection(), capture).Options);
            capture.Ids = db.Database.GetMigrations().ToArray();
            capture.Columns = db.Model.GetRelationalModel().Tables.SelectMany(t => t.Columns.Select(c => t.Name + "." + c.Name)).ToArray();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["Seeding:SuperAdmin:Email"] = "superadmin@example.invalid", ["Seeding:Admin:Email"] = "admin@example.invalid",
                ["Seeding:SuperAdmin:PreviousEmail"] = "legacy-superadmin@example.invalid",
                ["Seeding:SuperAdmin:Password"] = super, ["Seeding:Admin:Password"] = admin }).Build();
            var registrations = new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config)
                .AddSingleton<IUserAccountRepository>(Users).AddSingleton<IAuthIdentityRepository>(Identities)
                .AddSingleton<IUserProfileRepository>(Profiles).AddSingleton<IAdminRepository>(Admins).AddSingleton<IPasswordHasher>(Hasher);
            if (environment != null) registrations.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = environment });
            Services = registrations.BuildServiceProvider();
        }
        public UserAccount Existing(string email, RoleLevel role)
        {
            var user = new UserAccount("existing-hash", "09171234567");
            var identity = AuthIdentity.CreateManual(user.Id, email, "existing-hash", true);
            user.AuthIdentities.Add(identity); Identities.Identities.Add(identity);
            var profile = new UserProfile(user.Id, "Synthetic", "Admin", null, null);
            user.UserProfile = profile; Profiles.Profiles.Add(profile);
            profile.Admin = new Admin(profile, role); Admins.Admins.Add(profile.Admin);
            Users.Users.Add(user); return user;
        }
        public void Dispose() => Services.Dispose();
    }
    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct=default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }
    private sealed class History : DbCommandInterceptor
    {
        public string[] Ids { get; set; } = [];
        public string[] Columns { get; set; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct=default)
        {
            Assert.StartsWith("SELECT", command.CommandText.TrimStart());
            var table=new DataTable();
            if (command.CommandText.Contains("information_schema.columns"))
            {
                table.Columns.Add("Value",typeof(string));foreach(var column in Columns)table.Rows.Add(column);
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }
            table.Columns.Add("MigrationId",typeof(string));table.Columns.Add("ProductVersion",typeof(string));
            foreach(var id in Ids)table.Rows.Add(id,"10.0.7");
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }
    }
}
