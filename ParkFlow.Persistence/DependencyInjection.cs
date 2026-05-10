using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;
using ParkFlow.Persistence.Repositories;

namespace ParkFlow.Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IUserAccountRepository, UserAccountRepository>();
            services.AddScoped<IUserProfileRepository, UserProfileRepository>();
            return services;
        }
    }
}
