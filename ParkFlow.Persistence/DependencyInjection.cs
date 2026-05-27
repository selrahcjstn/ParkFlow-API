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
            services.AddScoped<IAuthIdentityRepository, AuthIdentityRepository>();
            services.AddScoped<IUserProfileRepository, UserProfileRepository>();
            services.AddScoped<ICorSubmissionRepository, CorSubmissionRepository>();
            services.AddScoped<IParkingScheduleRepository, ParkingScheduleRepository>();
            services.AddScoped<IVehicleRepository, VehicleRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IPersonnelRepository, PersonnelRepository>();
            services.AddScoped<IGuardRepository, GuardRepository>();
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IParkingLogRepository, ParkingLogRepository>();
            services.AddScoped<IViolationRepository, ViolationRepository>();
            return services;
        }
    }
}
