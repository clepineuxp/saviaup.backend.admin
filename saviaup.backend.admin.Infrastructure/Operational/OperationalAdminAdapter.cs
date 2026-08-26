using Microsoft.EntityFrameworkCore;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Infrastructure.Operational;

internal sealed class OperationalAdminAdapter(
    OperationalPlatformDbContext platformContext,
    OperationalApplicationDbContext applicationContext,
    IDateTimeProvider clock) : IOperationalAdminPort
{
    public async Task<OperationalCounts> GetCountsAsync(CancellationToken cancellationToken)
        => new(
            await platformContext.Users.AsNoTracking().CountAsync(cancellationToken),
            await platformContext.Users.AsNoTracking().CountAsync(item => item.IsActive, cancellationToken),
            await platformContext.Tenants.AsNoTracking().CountAsync(cancellationToken),
            await platformContext.Tenants.AsNoTracking().CountAsync(item => item.IsActive, cancellationToken));

    public async Task<IReadOnlyCollection<PlatformUserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await platformContext.Users.AsNoTracking().OrderBy(item => item.FirstName).ThenBy(item => item.LastName).ToArrayAsync(cancellationToken);
        return await MapUsersAsync(users, cancellationToken);
    }

    public async Task<PlatformUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await platformContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return null;
        return (await MapUsersAsync([user], cancellationToken)).Single();
    }

    public async Task<IReadOnlyCollection<OperationalOrganization>> GetOrganizationsAsync(CancellationToken cancellationToken)
    {
        var tenants = await platformContext.Tenants.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(cancellationToken);
        var tenantIds = tenants.Select(item => item.Id).ToArray();
        var memberships = await platformContext.Memberships.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToArrayAsync(cancellationToken);
        var users = await platformContext.Users.AsNoTracking().Where(item => memberships.Select(value => value.UserId).Contains(item.Id)).ToArrayAsync(cancellationToken);
        var roles = await applicationContext.Roles.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToArrayAsync(cancellationToken);
        var tenantPermissions = await platformContext.TenantPermissions.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToArrayAsync(cancellationToken);
        var permissions = await platformContext.Permissions.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Code, cancellationToken);
        var lastOrders = await applicationContext.Orders.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId))
            .GroupBy(item => item.TenantId).Select(group => new { TenantId = group.Key, LastAt = group.Max(item => item.CreatedAt) })
            .ToDictionaryAsync(item => item.TenantId, item => (DateTimeOffset?)item.LastAt, cancellationToken);
        var userById = users.ToDictionary(item => item.Id);
        var roleById = roles.ToDictionary(item => item.Id);

        return tenants.Select(tenant =>
        {
            var tenantMemberships = memberships.Where(item => item.TenantId == tenant.Id).ToArray();
            var ownerMembership = tenantMemberships.FirstOrDefault(item => roleById.GetValueOrDefault(item.RoleId)?.Code == "TENANT_OWNER" && IsEnabled(item));
            var ownerUser = ownerMembership is null ? null : userById.GetValueOrDefault(ownerMembership.UserId);
            var owner = ownerUser is null
                ? new OwnerSummaryDto(Guid.Empty, "Owner no disponible", tenant.Email ?? string.Empty)
                : new OwnerSummaryDto(ownerUser.Id, FullName(ownerUser), ownerUser.Email);
            var enabledCodes = tenantPermissions.Where(item => item.TenantId == tenant.Id)
                .Select(item => permissions.GetValueOrDefault(item.PermissionId)).Where(item => item is not null).Cast<string>().Order().ToArray();
            return new OperationalOrganization(
                tenant.Id,
                tenant.Name,
                tenant.ResponsibleName ?? tenant.Name,
                tenant.Document ?? string.Empty,
                tenant.Email ?? owner.Email,
                tenant.City ?? string.Empty,
                tenant.IsActive,
                tenant.CreatedAt,
                owner,
                tenantMemberships.Length,
                enabledCodes,
                permissions.Count,
                lastOrders.GetValueOrDefault(tenant.Id));
        }).ToArray();
    }

    public async Task<OperationalOrganizationDetail?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = (await GetOrganizationsAsync(cancellationToken)).SingleOrDefault(item => item.Id == organizationId);
        if (organization is null) return null;
        var memberships = await platformContext.Memberships.AsNoTracking().Where(item => item.TenantId == organizationId).ToArrayAsync(cancellationToken);
        var userIds = memberships.Select(item => item.UserId).Distinct().ToArray();
        var users = await platformContext.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var roleIds = memberships.Select(item => item.RoleId).Distinct().ToArray();
        var roles = await applicationContext.Roles.AsNoTracking().Where(item => roleIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var lastAccess = await LastAccessByUserAsync(userIds, cancellationToken);
        var members = memberships.Select(item =>
        {
            var user = users[item.UserId];
            var role = roles.GetValueOrDefault(item.RoleId);
            return new OrganizationMemberDto(item.Id, user.Id, FullName(user), user.Email, role?.Code ?? "UNKNOWN", role?.Name ?? "Rol no disponible",
                IsEnabled(item), item.DisabledUntil, lastAccess.GetValueOrDefault(user.Id));
        }).OrderByDescending(item => item.RoleCode == "TENANT_OWNER").ThenBy(item => item.Name).ToArray();
        return new OperationalOrganizationDetail(organization, members, await GetPermissionCatalogAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<OrganizationOperationDto>> GetOperationsAsync(
        OperationalStatusPolicy policy,
        CancellationToken cancellationToken)
    {
        var tenants = await platformContext.Tenants.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(cancellationToken);
        var tenantIds = tenants.Select(item => item.Id).ToArray();
        var today = new DateTimeOffset(clock.UtcNow.Year, clock.UtcNow.Month, clock.UtcNow.Day, 0, 0, 0, TimeSpan.Zero);
        var todayOrders = await applicationContext.Orders.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId) && item.CreatedAt >= today).ToArrayAsync(cancellationToken);
        var lastOrders = await applicationContext.Orders.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId))
            .GroupBy(item => item.TenantId).Select(group => new { TenantId = group.Key, LastAt = group.Max(item => item.CreatedAt) })
            .ToDictionaryAsync(item => item.TenantId, item => (DateTimeOffset?)item.LastAt, cancellationToken);
        var tables = await applicationContext.Tables.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToArrayAsync(cancellationToken);
        var openShifts = await applicationContext.CashRegisterShifts.AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId) && item.Status == "OPEN" && item.ClosedAt == null).ToArrayAsync(cancellationToken);

        return tenants.Select(tenant =>
        {
            var orders = todayOrders.Where(item => item.TenantId == tenant.Id).ToArray();
            var paid = orders.Where(item => item.Status == "PAID").ToArray();
            var sales = paid.Sum(item => item.TotalAmount);
            var tenantTables = tables.Where(item => item.TenantId == tenant.Id && !item.IsCashRegister).ToArray();
            var occupied = tenantTables.Count(item => item.Status == "OCCUPIED");
            var openCash = openShifts.Count(item => item.TenantId == tenant.Id);
            var lastOrder = lastOrders.GetValueOrDefault(tenant.Id);
            var issues = BuildIssues(tenant, orders.Length, openCash, lastOrder, policy);
            var health = !tenant.IsActive ? "INACTIVE" : issues.Any(item => item.Severity == "CRITICAL") ? "CRITICAL" : issues.Count > 0 ? "ATTENTION" : "HEALTHY";
            var apiStatus = !tenant.IsActive ? "OFFLINE" : health == "CRITICAL" ? "DEGRADED" : "ONLINE";
            return new OrganizationOperationDto(
                tenant.Id,
                tenant.Name,
                health,
                apiStatus,
                sales,
                orders.Length,
                orders.Length == 0 ? 0 : decimal.Round(sales / orders.Length, 2),
                occupied,
                tenantTables.Length,
                openCash,
                !tenant.RequiresOpenCashRegister ? "NOT_REQUIRED" : openCash > 0 ? "OPEN" : "CLOSED",
                lastOrder,
                issues);
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<OperationalPermission>> GetPermissionCatalogAsync(CancellationToken cancellationToken)
        => await platformContext.Permissions.AsNoTracking()
            .OrderBy(item => item.Module.Code)
            .ThenBy(item => item.Code)
            .Select(item => new OperationalPermission(
                item.Id, item.Code, item.Description, item.Module.Code, item.Module.Name, item.Module.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<string>> GetTenantPermissionCodesAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
        => await platformContext.TenantPermissions.AsNoTracking()
            .Where(item => item.TenantId == organizationId)
            .Join(
                platformContext.Permissions.AsNoTracking(),
                tenantPermission => tenantPermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Code)
            .OrderBy(code => code)
            .ToArrayAsync(cancellationToken);

    public Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken)
        => platformContext.Tenants.AsNoTracking().AnyAsync(item => item.Id == organizationId, cancellationToken);

    public async Task ReplaceTenantPermissionsAsync(Guid organizationId, IReadOnlyCollection<string> permissionCodes, CancellationToken cancellationToken)
    {
        var tenantExists = await platformContext.Tenants.AnyAsync(item => item.Id == organizationId, cancellationToken);
        if (!tenantExists) throw new InvalidOperationException(AdminErrors.OrganizationNotFound.Code);
        var uniqueCodes = permissionCodes.Distinct(StringComparer.Ordinal).ToArray();
        var permissionIds = await platformContext.Permissions.AsNoTracking().Where(item => uniqueCodes.Contains(item.Code))
            .Select(item => item.Id).ToArrayAsync(cancellationToken);
        if (permissionIds.Length != uniqueCodes.Length) throw new InvalidOperationException(AdminErrors.PermissionNotFound.Code);

        await using var transaction = await platformContext.Database.BeginTransactionAsync(cancellationToken);
        var current = await platformContext.TenantPermissions.Where(item => item.TenantId == organizationId).ToArrayAsync(cancellationToken);
        platformContext.TenantPermissions.RemoveRange(current);
        await platformContext.TenantPermissions.AddRangeAsync(permissionIds.Select(id => new OperationalTenantPermission
        {
            TenantId = organizationId,
            PermissionId = id
        }), cancellationToken);
        await platformContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> SetOrganizationStatusAsync(Guid organizationId, bool isActive, CancellationToken cancellationToken)
    {
        var tenant = await platformContext.Tenants.SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);
        if (tenant is null) return false;
        tenant.IsActive = isActive;
        tenant.UpdatedAt = clock.UtcNow;
        await platformContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Result> ChangeOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await platformContext.Tenants.AnyAsync(item => item.Id == organizationId, cancellationToken))
            return Result.Failure(AdminErrors.OrganizationNotFound);
        var roles = await applicationContext.Roles.AsNoTracking().Where(item => item.TenantId == organizationId && item.IsActive).ToArrayAsync(cancellationToken);
        var ownerRole = roles.SingleOrDefault(item => item.Code == "TENANT_OWNER");
        if (ownerRole is null) return Result.Failure(AdminErrors.OwnerFallbackRoleMissing);
        var memberships = await platformContext.Memberships.Where(item => item.TenantId == organizationId).ToArrayAsync(cancellationToken);
        var target = memberships.SingleOrDefault(item => item.UserId == userId);
        if (target is null || !IsEnabled(target)) return Result.Failure(AdminErrors.OwnerMustBeActiveMember);
        if (target.RoleId == ownerRole.Id) return Result.Success();
        var fallback = roles.FirstOrDefault(item => item.Code == "MANAGER") ?? roles.FirstOrDefault(item => item.Id != ownerRole.Id && !item.IsSystem)
            ?? roles.FirstOrDefault(item => item.Id != ownerRole.Id);
        if (fallback is null) return Result.Failure(AdminErrors.OwnerFallbackRoleMissing);

        await using var transaction = await platformContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var previousOwner in memberships.Where(item => item.RoleId == ownerRole.Id)) previousOwner.RoleId = fallback.Id;
        target.RoleId = ownerRole.Id;
        target.IsActive = true;
        target.DisabledUntil = null;
        await platformContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<(string Email, string PreferredLanguage)?> GetPasswordResetRecipientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await platformContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);
        return user is null ? null : (user.Email, user.PreferredLanguage);
    }

    public async Task AddPasswordResetTokenAsync(Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await platformContext.PasswordResetTokens.AddAsync(new OperationalPasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        }, cancellationToken);
        await platformContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<Guid>> SetMembershipStatusAsync(Guid membershipId, bool isActive, CancellationToken cancellationToken)
    {
        var membership = await platformContext.Memberships.SingleOrDefaultAsync(item => item.Id == membershipId, cancellationToken);
        if (membership is null) return Result<Guid>.Failure(AdminErrors.MembershipNotFound);
        var isOwner = await applicationContext.Roles.AsNoTracking().AnyAsync(item => item.Id == membership.RoleId && item.Code == "TENANT_OWNER", cancellationToken);
        if (!isActive && isOwner) return Result<Guid>.Failure(AdminErrors.OwnerCannotBeDisabled);
        membership.IsActive = isActive;
        membership.DisabledUntil = null;
        await platformContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(membership.UserId);
    }

    public async Task<Result<Guid>> ReassignMembershipAsync(Guid userId, Guid membershipId, Guid targetOrganizationId, CancellationToken cancellationToken)
    {
        var membership = await platformContext.Memberships.SingleOrDefaultAsync(item => item.Id == membershipId && item.UserId == userId, cancellationToken);
        if (membership is null) return Result<Guid>.Failure(AdminErrors.MembershipNotFound);
        var sourceRole = await applicationContext.Roles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == membership.RoleId, cancellationToken);
        if (sourceRole?.Code == "TENANT_OWNER") return Result<Guid>.Failure(AdminErrors.OwnerCannotBeReassigned);
        if (!await platformContext.Tenants.AnyAsync(item => item.Id == targetOrganizationId, cancellationToken))
            return Result<Guid>.Failure(AdminErrors.OrganizationNotFound);
        if (await platformContext.Memberships.AnyAsync(item => item.UserId == userId && item.TenantId == targetOrganizationId, cancellationToken))
            return Result<Guid>.Failure(AdminErrors.MembershipAlreadyExists);
        var targetRoles = await applicationContext.Roles.AsNoTracking().Where(item => item.TenantId == targetOrganizationId && item.IsActive && item.Code != "TENANT_OWNER").ToArrayAsync(cancellationToken);
        var targetRole = targetRoles.FirstOrDefault(item => item.Code == "MANAGER") ?? targetRoles.FirstOrDefault(item => !item.IsSystem) ?? targetRoles.FirstOrDefault();
        if (targetRole is null) return Result<Guid>.Failure(AdminErrors.OwnerFallbackRoleMissing);

        membership.TenantId = targetOrganizationId;
        membership.RoleId = targetRole.Id;
        membership.IsActive = true;
        membership.DisabledUntil = null;
        await platformContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(userId);
    }

    private async Task<IReadOnlyCollection<PlatformUserDto>> MapUsersAsync(IReadOnlyCollection<OperationalUser> users, CancellationToken cancellationToken)
    {
        var userIds = users.Select(item => item.Id).ToArray();
        var memberships = await platformContext.Memberships.AsNoTracking().Where(item => userIds.Contains(item.UserId)).ToArrayAsync(cancellationToken);
        var tenantIds = memberships.Select(item => item.TenantId).Distinct().ToArray();
        var roleIds = memberships.Select(item => item.RoleId).Distinct().ToArray();
        var tenants = await platformContext.Tenants.AsNoTracking().Where(item => tenantIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var roles = await applicationContext.Roles.AsNoTracking().Where(item => roleIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var lastAccess = await LastAccessByUserAsync(userIds, cancellationToken);
        return users.Select(user => new PlatformUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.IsActive,
            user.CreatedAt,
            lastAccess.GetValueOrDefault(user.Id),
            memberships.Where(item => item.UserId == user.Id).Select(item => new MembershipSummaryDto(
                item.Id,
                item.TenantId,
                tenants.GetValueOrDefault(item.TenantId)?.Name ?? "Organización no disponible",
                roles.GetValueOrDefault(item.RoleId)?.Code ?? "UNKNOWN",
                roles.GetValueOrDefault(item.RoleId)?.Name ?? "Rol no disponible",
                IsEnabled(item),
                item.DisabledUntil)).OrderBy(item => item.OrganizationName).ToArray())).ToArray();
    }

    private async Task<Dictionary<Guid, DateTimeOffset?>> LastAccessByUserAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        => await platformContext.RefreshTokens.AsNoTracking().Where(item => userIds.Contains(item.UserId))
            .GroupBy(item => item.UserId).Select(group => new { UserId = group.Key, LastAt = group.Max(item => item.CreatedAt) })
            .ToDictionaryAsync(item => item.UserId, item => (DateTimeOffset?)item.LastAt, cancellationToken);

    private IReadOnlyCollection<OperationIssueDto> BuildIssues(
        OperationalTenant tenant,
        int todayOrders,
        int openCash,
        DateTimeOffset? lastOrder,
        OperationalStatusPolicy policy)
    {
        if (!tenant.IsActive) return [];
        var issues = new List<OperationIssueDto>();
        if (policy.CashRegisterRuleEnabled && tenant.RequiresOpenCashRegister && openCash == 0)
            issues.Add(new OperationIssueDto(policy.CashRegisterSeverity, "No hay una caja abierta para la operación."));
        if (policy.InactivityRuleEnabled && todayOrders > 0 && lastOrder < clock.UtcNow.AddMinutes(-policy.InactivityThresholdMinutes))
            issues.Add(new OperationIssueDto(
                policy.InactivitySeverity,
                $"No hay actividad de órdenes en los últimos {FormatThreshold(policy.InactivityThresholdMinutes)}."));
        return issues;
    }

    private static string FormatThreshold(int minutes)
        => minutes % 60 == 0
            ? minutes == 60 ? "60 minutos (1 hora)" : $"{minutes} minutos ({minutes / 60} horas)"
            : $"{minutes} minutos";

    private bool IsEnabled(OperationalMembership membership)
        => membership.IsActive || membership.DisabledUntil <= clock.UtcNow;

    private static string FullName(OperationalUser user) => $"{user.FirstName} {user.LastName}".Trim();
}
