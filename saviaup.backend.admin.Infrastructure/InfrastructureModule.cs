using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaviaUp.Admin.Application.Options;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Infrastructure.Bootstrap;
using SaviaUp.Admin.Infrastructure.Email;
using SaviaUp.Admin.Infrastructure.Health;
using SaviaUp.Admin.Infrastructure.Operational;
using SaviaUp.Admin.Infrastructure.Persistence;
using SaviaUp.Admin.Infrastructure.Persistence.Repositories;
using SaviaUp.Admin.Infrastructure.Security;

namespace SaviaUp.Admin.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var adminConnection = RequiredConnection(configuration, "AdminDatabase");
        var platformConnection = RequiredConnection(configuration, "PlatformDatabase");
        var applicationConnection = RequiredConnection(configuration, "ApplicationDatabase");
        services.AddDbContext<AdminDbContext>(options => options.UseNpgsql(adminConnection, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddDbContext<OperationalPlatformDbContext>(options => options.UseNpgsql(platformConnection, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddDbContext<OperationalApplicationDbContext>(options => options.UseNpgsql(applicationConnection, npgsql => npgsql.EnableRetryOnFailure()));

        services.Configure<AdminJwtOptions>(configuration.GetSection(AdminJwtOptions.SectionName));
        services.Configure<OperationalFrontendOptions>(configuration.GetSection(OperationalFrontendOptions.SectionName));
        services.Configure<BootstrapAdminOptions>(configuration.GetSection(BootstrapAdminOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        services.AddScoped<IAdminIdentityRepository, AdminIdentityRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IOperationStatusSettingsRepository, OperationStatusSettingsRepository>();
        services.AddScoped<IAdminUnitOfWork, AdminUnitOfWork>();
        services.AddScoped<IOperationalAdminPort, OperationalAdminAdapter>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IAdminPasswordHasher, AdminPasswordHasher>();
        services.AddSingleton<IAdminTokenIssuer, AdminTokenIssuer>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IPasswordResetLinkFactory, PasswordResetLinkFactory>();

        if (string.Equals(configuration[$"{EmailOptions.SectionName}:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IAdminEmailSender, SmtpAdminEmailSender>();
        else
            services.AddSingleton<IAdminEmailSender, DevelopmentAdminEmailSender>();

        services.AddHostedService<AdminDatabaseInitializer>();
        services.AddHealthChecks().AddCheck<AdminDatabasesHealthCheck>("required-databases");
        return services;
    }

    private static string RequiredConnection(IConfiguration configuration, string name)
    {
        var connection = configuration.GetConnectionString(name);
        return !string.IsNullOrWhiteSpace(connection)
            ? connection
            : throw new InvalidOperationException($"ConnectionStrings:{name} is required.");
    }
}
