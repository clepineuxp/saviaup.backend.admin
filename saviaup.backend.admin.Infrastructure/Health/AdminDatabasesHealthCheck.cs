using Microsoft.Extensions.Diagnostics.HealthChecks;
using SaviaUp.Admin.Infrastructure.Operational;
using SaviaUp.Admin.Infrastructure.Persistence;

namespace SaviaUp.Admin.Infrastructure.Health;

internal sealed class AdminDatabasesHealthCheck(
    AdminDbContext adminContext,
    OperationalPlatformDbContext platformContext,
    OperationalApplicationDbContext applicationContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var admin = await adminContext.Database.CanConnectAsync(cancellationToken);
            var platform = await platformContext.Database.CanConnectAsync(cancellationToken);
            var application = await applicationContext.Database.CanConnectAsync(cancellationToken);
            return admin && platform && application
                ? HealthCheckResult.Healthy("Las tres bases requeridas están disponibles.")
                : HealthCheckResult.Unhealthy("Una o más bases requeridas no están disponibles.", data: new Dictionary<string, object>
                {
                    ["adminDatabase"] = admin,
                    ["platformDatabase"] = platform,
                    ["applicationDatabase"] = application
                });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("No fue posible comprobar las bases requeridas.", exception);
        }
    }
}
