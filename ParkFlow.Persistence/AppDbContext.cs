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
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<CorSubmission> CorSubmissions { get; set; }
        public DbSet<ParkingSchedule> ParkingSchedules { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Guard> Guards { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Personnel> Personnel { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}