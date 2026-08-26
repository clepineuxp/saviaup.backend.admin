using Microsoft.EntityFrameworkCore;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Infrastructure.Persistence.Repositories;

public sealed class AdminIdentityRepository(AdminDbContext context) : IAdminIdentityRepository
{
    public Task<AdminUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => context.AdminUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.AdminUsers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<AdminUser>> ListAsync(CancellationToken cancellationToken)
        => await context.AdminUsers.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(cancellationToken);

    public async Task AddAsync(AdminUser user, CancellationToken cancellationToken)
        => await context.AdminUsers.AddAsync(user, cancellationToken);
}

public sealed class PlanRepository(AdminDbContext context) : IPlanRepository
{
    public async Task<IReadOnlyCollection<Plan>> ListAsync(CancellationToken cancellationToken)
        => await context.Plans.AsNoTracking().Include(item => item.Permissions).Include(item => item.PriceHistory)
            .OrderBy(item => item.MonthlyPrice).ThenBy(item => item.Name).ToArrayAsync(cancellationToken);

    public Task<Plan?> GetAsync(Guid id, CancellationToken cancellationToken)
        => context.Plans.Include(item => item.Permissions).Include(item => item.PriceHistory)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludedId, CancellationToken cancellationToken)
        => context.Plans.AnyAsync(item => item.Code == code && item.Id != excludedId, cancellationToken);

    public async Task AddAsync(Plan plan, CancellationToken cancellationToken)
        => await context.Plans.AddAsync(plan, cancellationToken);

    public Task<TenantPlanAssignment?> GetAssignmentAsync(Guid tenantId, CancellationToken cancellationToken)
        => context.TenantPlanAssignments.Include(item => item.Plan).ThenInclude(plan => plan!.Permissions)
            .Include(item => item.Overrides).SingleOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyCollection<TenantPlanAssignment>> GetAssignmentsForPlanAsync(Guid planId, CancellationToken cancellationToken)
        => await context.TenantPlanAssignments.Include(item => item.Plan).ThenInclude(plan => plan!.Permissions)
            .Include(item => item.Overrides).Where(item => item.PlanId == planId).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, TenantPlanAssignment>> GetAssignmentsAsync(CancellationToken cancellationToken)
        => await context.TenantPlanAssignments.AsNoTracking().Include(item => item.Plan).ThenInclude(plan => plan!.Permissions)
            .Include(item => item.Overrides).ToDictionaryAsync(item => item.TenantId, cancellationToken);

    public async Task AddAssignmentAsync(TenantPlanAssignment assignment, CancellationToken cancellationToken)
        => await context.TenantPlanAssignments.AddAsync(assignment, cancellationToken);

    public Task<int> CountAssignmentsAsync(Guid planId, CancellationToken cancellationToken)
        => context.TenantPlanAssignments.CountAsync(item => item.PlanId == planId, cancellationToken);
}

public sealed class AuditRepository(AdminDbContext context) : IAuditRepository
{
    public async Task AddAsync(AdminAuditLog entry, CancellationToken cancellationToken)
        => await context.AuditLogs.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyCollection<AdminAuditLog>> ListRecentAsync(int take, CancellationToken cancellationToken)
        => await context.AuditLogs.AsNoTracking().OrderByDescending(item => item.OccurredAt).Take(take).ToArrayAsync(cancellationToken);
}

public sealed class OperationStatusSettingsRepository(AdminDbContext context) : IOperationStatusSettingsRepository
{
    public Task<OperationStatusSettings?> GetAsync(CancellationToken cancellationToken)
        => context.OperationStatusSettings.SingleOrDefaultAsync(
            item => item.Id == OperationStatusSettings.SingletonId,
            cancellationToken);

    public async Task AddAsync(OperationStatusSettings settings, CancellationToken cancellationToken)
        => await context.OperationStatusSettings.AddAsync(settings, cancellationToken);
}

public sealed class AdminUnitOfWork(AdminDbContext context) : IAdminUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
