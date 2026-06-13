using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Infrastructure.Emails;
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
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));

        services.AddDbContext<PhoneGrapherDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IVnPayService, VnPayService>();
        // Chọn nhà cung cấp email: Brevo (HTTP API, chạy được trên Render free) nếu bật, ngược lại dùng SMTP (Gmail – cho local).
        services.AddScoped<SmtpEmailService>();
        services.AddScoped<BrevoEmailService>();
        services.AddScoped<IEmailService>(sp =>
            sp.GetRequiredService<IOptions<BrevoOptions>>().Value.Enabled
                ? sp.GetRequiredService<BrevoEmailService>()
                : sp.GetRequiredService<SmtpEmailService>());
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IGrapherService, GrapherService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
