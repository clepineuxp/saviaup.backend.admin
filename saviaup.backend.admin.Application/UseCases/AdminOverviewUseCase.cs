using SaviaUp.Admin.Application.Common;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.UseCases;

public sealed class AdminOverviewUseCase(
    IOperationalAdminPort operationalPort,
    IPlanRepository planRepository,
    IAuditRepository auditRepository,
    IDateTimeProvider clock) : IAdminOverviewUseCase
{
    public async Task<Result<DashboardSnapshotDto>> GetDashboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            var countsTask = operationalPort.GetCountsAsync(cancellationToken);
            var organizationsTask = operationalPort.GetOrganizationsAsync(cancellationToken);
            var operationsTask = operationalPort.GetOperationsAsync(cancellationToken);
            var assignmentsTask = planRepository.GetAssignmentsAsync(cancellationToken);
            var auditTask = auditRepository.ListRecentAsync(8, cancellationToken);
            await Task.WhenAll(countsTask, organizationsTask, operationsTask, assignmentsTask, auditTask);

            var counts = await countsTask;
            var organizations = await organizationsTask;
            var operations = await operationsTask;
            var assignments = await assignmentsTask;
            var audits = await auditTask;
            var mrr = organizations.Where(item => item.IsActive)
                .Sum(item => assignments.TryGetValue(item.Id, out var assignment) && assignment.Plan?.Status != PlanStatuses.Archived
                    ? assignment.Plan?.MonthlyPrice ?? 0
                    : 0);
            var withAlerts = operations.Count(item => item.Issues.Count > 0 && item.Health != "INACTIVE");
            var firstCurrentMonth = new DateTimeOffset(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var firstPreviousMonth = firstCurrentMonth.AddMonths(-1);
            var currentCreated = organizations.Count(item => item.CreatedAt >= firstCurrentMonth);
            var previousCreated = organizations.Count(item => item.CreatedAt >= firstPreviousMonth && item.CreatedAt < firstCurrentMonth);
            var growth = previousCreated == 0 ? (currentCreated == 0 ? 0 : 100) : decimal.Round((currentCreated - previousCreated) * 100m / previousCreated, 1);

            return Result<DashboardSnapshotDto>.Success(new DashboardSnapshotDto(
                counts.TotalUsers,
                counts.ActiveUsers,
                counts.TotalOrganizations,
                counts.ActiveOrganizations,
                withAlerts,
                operations.Sum(item => item.TodaySales),
                operations.Sum(item => item.TodayOrders),
                mrr,
                growth,
                audits.Select(MapAudit).ToArray(),
                operations));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result<DashboardSnapshotDto>.Failure(AdminErrors.OperationalUnavailable);
        }
    }

    public async Task<Result<IReadOnlyCollection<PlatformUserDto>>> GetUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Result<IReadOnlyCollection<PlatformUserDto>>.Success(await operationalPort.GetUsersAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result<IReadOnlyCollection<PlatformUserDto>>.Failure(AdminErrors.OperationalUnavailable); }
    }

    public async Task<Result<IReadOnlyCollection<OrganizationSummaryDto>>> GetOrganizationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var organizationsTask = operationalPort.GetOrganizationsAsync(cancellationToken);
            var assignmentsTask = planRepository.GetAssignmentsAsync(cancellationToken);
            var operationsTask = operationalPort.GetOperationsAsync(cancellationToken);
            await Task.WhenAll(organizationsTask, assignmentsTask, operationsTask);
            var assignments = await assignmentsTask;
            var operationByTenant = (await operationsTask).ToDictionary(item => item.OrganizationId);
            var mapped = (await organizationsTask).Select(item => MapOrganization(
                item,
                assignments.GetValueOrDefault(item.Id),
                operationByTenant.GetValueOrDefault(item.Id)?.Health ?? (item.IsActive ? "ATTENTION" : "INACTIVE"),
                assignments.GetValueOrDefault(item.Id)?.Plan)).ToArray();
            return Result<IReadOnlyCollection<OrganizationSummaryDto>>.Success(mapped);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result<IReadOnlyCollection<OrganizationSummaryDto>>.Failure(AdminErrors.OperationalUnavailable); }
    }

    public async Task<Result<OrganizationDetailDto>> GetOrganizationAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var detailTask = operationalPort.GetOrganizationAsync(id, cancellationToken);
            var assignmentTask = planRepository.GetAssignmentAsync(id, cancellationToken);
            var operationsTask = operationalPort.GetOperationsAsync(cancellationToken);
            await Task.WhenAll(detailTask, assignmentTask, operationsTask);
            var operational = await detailTask;
            if (operational is null) return Result<OrganizationDetailDto>.Failure(AdminErrors.OrganizationNotFound);
            var assignment = await assignmentTask;
            var operation = (await operationsTask).SingleOrDefault(item => item.OrganizationId == id);
            var summary = MapOrganization(operational.Organization, assignment, operation?.Health ?? (operational.Organization.IsActive ? "ATTENTION" : "INACTIVE"), assignment?.Plan);
            var enabled = operational.Organization.EnabledPermissionCodes.ToHashSet(StringComparer.Ordinal);
            var permissions = operational.PermissionCatalog.OrderBy(item => item.ModuleName).ThenBy(item => item.Code)
                .Select(item => MapPermission(item, enabled.Contains(item.Code))).ToArray();
            return Result<OrganizationDetailDto>.Success(new OrganizationDetailDto(
                summary.Id,
                summary.Name,
                summary.LegalName,
                summary.Slug,
                summary.IsActive,
                summary.Owner,
                summary.MemberCount,
                summary.ActivePermissionCount,
                operational.PermissionCatalog.Count,
                summary.Plan,
                summary.CreatedAt,
                summary.LastActivityAt,
                summary.Health,
                operational.Organization.DocumentNumber,
                operational.Organization.ContactEmail,
                operational.Organization.City,
                permissions,
                operational.Members,
                assignment?.SyncStatus ?? "UNMANAGED",
                assignment?.LastSyncedAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result<OrganizationDetailDto>.Failure(AdminErrors.OperationalUnavailable); }
    }

    public async Task<Result<IReadOnlyCollection<OrganizationOperationDto>>> GetOperationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Result<IReadOnlyCollection<OrganizationOperationDto>>.Success(await operationalPort.GetOperationsAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result<IReadOnlyCollection<OrganizationOperationDto>>.Failure(AdminErrors.OperationalUnavailable); }
    }

    private static OrganizationSummaryDto MapOrganization(
        OperationalOrganization organization,
        TenantPlanAssignment? assignment,
        string health,
        Plan? plan)
        => new(
            organization.Id,
            organization.Name,
            organization.LegalName,
            ApplicationHelpers.Slugify(organization.Name),
            organization.IsActive,
            organization.Owner,
            organization.MemberCount,
            organization.EnabledPermissionCodes.Count,
            organization.TotalPermissionCount,
            plan is null ? null : new PlanSummaryDto(plan.Id, plan.Name, plan.MonthlyPrice, plan.Currency),
            organization.CreatedAt,
            organization.LastActivityAt,
            health);

    private static TenantPermissionDto MapPermission(OperationalPermission permission, bool enabled)
    {
        var group = permission.ModuleCode switch
        {
            "tables" => ("sales", "Ventas"),
            "orders" or "statistics" or "billing" => ("operation", "Operación"),
            "products" or "categories" => ("catalog", "Catálogo"),
            "inventory" => ("inventory", "Inventario"),
            "cash_registers" => ("cash-registers", "Cajas"),
            "settings" => ("settings", "Configuración"),
            _ => (permission.ModuleCode, permission.ModuleName)
        };
        return new TenantPermissionDto(
            permission.Code,
            FriendlyPermissionName(permission.Code),
            permission.Description == permission.Code ? FriendlyPermissionDescription(permission.Code) : permission.Description,
            group.Item1,
            group.Item2,
            enabled);
    }

    private static string FriendlyPermissionName(string code)
    {
        var resource = code.Split('.')[0] switch
        {
            "orders" => "órdenes",
            "tables" => "mesas",
            "inventory" => "inventario",
            "products" => "productos",
            "categories" => "categorías",
            "billing" => "facturación",
            "cash-registers" => "cajas",
            "settings" => "configuración",
            "reports" => "reportes",
            "kitchen" => "cocina",
            _ => code.Split('.')[0]
        };
        var action = code.Split('.').Last() switch
        {
            "read" => "Consultar",
            "manage" => "Gestionar",
            "operate" => "Operar",
            "create" => "Crear",
            "cancel" => "Anular",
            _ => "Usar"
        };
        return $"{action} {resource}";
    }

    private static string FriendlyPermissionDescription(string code) => $"Habilita la capacidad operacional {code}.";

    private static AuditEventDto MapAudit(AdminAuditLog item)
        => new(item.Id, item.Action, item.SubjectName, item.Detail, item.OccurredAt, item.Kind);
}
