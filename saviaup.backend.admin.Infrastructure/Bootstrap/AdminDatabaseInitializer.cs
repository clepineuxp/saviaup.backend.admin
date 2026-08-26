using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaviaUp.Admin.Application.Common;
using SaviaUp.Admin.Application.Options;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Entities;
using SaviaUp.Admin.Infrastructure.Persistence;

namespace SaviaUp.Admin.Infrastructure.Bootstrap;

internal sealed class AdminDatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IOptions<BootstrapAdminOptions> bootstrapOptions,
    ILogger<AdminDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        if (configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            await context.Database.MigrateAsync(cancellationToken);

        var options = bootstrapOptions.Value;
        if (!options.Enabled) return;
        Validate(options);
        var normalizedEmail = ApplicationHelpers.NormalizeEmail(options.Email);
        if (await context.AdminUsers.AnyAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken)) return;
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var hasher = scope.ServiceProvider.GetRequiredService<IAdminPasswordHasher>();
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Name = options.Name.Trim(),
            Email = options.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            RoleCode = AdminRoleCodes.SuperAdmin,
            IsActive = true,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        user.PasswordHash = hasher.Hash(user, options.Password);
        await context.AdminUsers.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrap administrative account created for {EmailDomain}. The password was not logged.", EmailDomain(user.Email));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Validate(BootstrapAdminOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Name) || string.IsNullOrWhiteSpace(options.Email)
            || options.Password.Length < 12 || !options.Password.Any(char.IsUpper) || !options.Password.Any(char.IsLower)
            || !options.Password.Any(char.IsDigit) || !options.Password.Any(character => !char.IsLetterOrDigit(character)))
            throw new InvalidOperationException("BootstrapAdmin configuration is incomplete or its password does not meet the policy.");
    }

    private static string EmailDomain(string email)
    {
        var separator = email.LastIndexOf('@');
        return separator < 0 ? "unknown" : email[separator..];
    }
}
