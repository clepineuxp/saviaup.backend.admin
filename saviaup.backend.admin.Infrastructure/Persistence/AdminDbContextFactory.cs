using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SaviaUp.Admin.Infrastructure.Persistence;

public sealed class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiDirectory = new[]
        {
            Path.Combine(currentDirectory, "saviaup.backend.admin.Api"),
            Path.Combine(currentDirectory, "..", "saviaup.backend.admin.Api"),
            currentDirectory
        }
        .Select(Path.GetFullPath)
        .FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json")))
        ?? currentDirectory;

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<AdminDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var environmentConnection = Environment.GetEnvironmentVariable("SAVIAUP_ADMIN_CONNECTION");
        var configuredConnection = configuration.GetConnectionString("AdminDatabase");
        var connection = !string.IsNullOrWhiteSpace(environmentConnection)
            ? environmentConnection
            : !string.IsNullOrWhiteSpace(configuredConnection)
                ? configuredConnection
                : throw new InvalidOperationException(
                    "Configure ConnectionStrings:AdminDatabase in User Secrets or SAVIAUP_ADMIN_CONNECTION before using EF Core tools.");

        var options = new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql(connection).Options;
        return new AdminDbContext(options);
    }
}
