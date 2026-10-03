using Microsoft.EntityFrameworkCore;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<AuthIdentity> AuthIdentities { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<CorSubmission> CorSubmissions { get; set; }
        public DbSet<ParkingSchedule> ParkingSchedules { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Violation> Violations { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Guard> Guards { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Personnel> Personnel { get; set; }
        public DbSet<ParkingLog> ParkingLogs { get; set; }
        public DbSet<EmailOtp> EmailOtps { get; set; }
        public DbSet<PasswordHistory> PasswordHistories { get; set; }
        public DbSet<ParkingReservation> ParkingReservations { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<SystemAnnouncement> SystemAnnouncements { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await FixUnsavedChildEntitiesAsync(cancellationToken);
            return await base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            FixUnsavedChildEntities();
            return base.SaveChanges();
        }

        private async Task FixUnsavedChildEntitiesAsync(CancellationToken cancellationToken)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is PasswordHistory history)
                    {
                        var exists = await PasswordHistories.AsNoTracking().AnyAsync(p => p.Id == history.Id, cancellationToken);
                        if (!exists)
                        {
                            entry.State = EntityState.Added;
                        }
                    }
                    else if (entry.Entity is AuthIdentity identity)
                    {
                        var exists = await AuthIdentities.AsNoTracking().AnyAsync(i => i.Id == identity.Id, cancellationToken);
                        if (!exists)
                        {
                            entry.State = EntityState.Added;
                        }
                    }
                }
            }
        }

        private void FixUnsavedChildEntities()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is PasswordHistory history)
                    {
                        var exists = PasswordHistories.AsNoTracking().Any(p => p.Id == history.Id);
                        if (!exists)
                        {
                            entry.State = EntityState.Added;
                        }
                    }
                    else if (entry.Entity is AuthIdentity identity)
                    {
                        var exists = AuthIdentities.AsNoTracking().Any(i => i.Id == identity.Id);
                        if (!exists)
                        {
                            entry.State = EntityState.Added;
                        }
                    }
                }
            }
        }
    }
}