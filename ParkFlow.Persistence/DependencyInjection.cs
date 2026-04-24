using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ParkFlow.Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql("Host=localhost;Port=5432;Database=parkflow;Username=postgres;Password=1234"));
            return services;
        }
    }
}
