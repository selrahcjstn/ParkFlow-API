using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Features.ParkingLogs.Services;
using System.Reflection;

namespace ParkFlow.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Register MediatR handlers from the assembly
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

            // Register all validators from the assembly
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddScoped<IParkingService, ParkingService>();
            services.AddScoped<IViolationService, ViolationService>();
            services.AddScoped<IScheduleService, ScheduleService>();
            services.AddScoped<IParkingLogRoleService, ParkingLogRoleService>();

            return services;
        }
    }
}
