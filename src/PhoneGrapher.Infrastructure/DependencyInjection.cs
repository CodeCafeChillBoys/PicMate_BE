using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Payments;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Security;
using PhoneGrapher.Infrastructure.Services;

namespace PhoneGrapher.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<VnPayOptions>(configuration.GetSection(VnPayOptions.SectionName));

        services.AddDbContext<PhoneGrapherDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IGrapherService, GrapherService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
