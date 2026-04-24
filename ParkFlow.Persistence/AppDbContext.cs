using Microsoft.EntityFrameworkCore;

namespace ParkFlow.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        //DB Sets go jere :>>
        public DbSet<UserAccount> UserAccounts { get; set; }
    }
}
