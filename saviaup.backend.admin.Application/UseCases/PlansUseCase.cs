using SaviaUp.Admin.Application.Common;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.UseCases;

public sealed class PlansUseCase(
    IPlanRepository repository,
    IOperationalAdminPort operationalPort,
    IAuditRepository auditRepository,
    IAdminActorContext actor,
    IDateTimeProvider clock,
    IAdminUnitOfWork unitOfWork) : IPlansUseCase
{
    public async Task<Result<IReadOnlyCollection<PlatformPlanDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var plans = await repository.ListAsync(cancellationToken);
        var result = new List<PlatformPlanDto>(plans.Count);
        foreach (var plan in plans)
        {
            result.Add(new PlatformPlanDto(
                plan.Id,
                plan.Code,
                plan.Name,
                plan.Description,
                plan.MonthlyPrice,
                plan.Currency,
                plan.Status,
                await repository.CountAssignmentsAsync(plan.Id, cancellationToken),
                plan.Permissions.Count));
        }

        return Result<IReadOnlyCollection<PlatformPlanDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyCollection<PlanPermissionOptionDto>>> GetPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await operationalPort.GetPermissionCatalogAsync(cancellationToken);
            return Result<IReadOnlyCollection<PlanPermissionOptionDto>>.Success(catalog.Where(item => item.ModuleIsActive)
                .Select(item => new PlanPermissionOptionDto(item.Code, item.Description, item.ModuleCode, item.ModuleName))
                .OrderBy(item => item.ModuleName).ThenBy(item => item.Code).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result<IReadOnlyCollection<PlanPermissionOptionDto>>.Failure(AdminErrors.OperationalUnavailable); }
    }

    public async Task<Result<PlanDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await repository.GetAsync(id, cancellationToken);
        return plan is null
            ? Result<PlanDetailDto>.Failure(AdminErrors.PlanNotFound)
            : Result<PlanDetailDto>.Success(await MapDetailAsync(plan, cancellationToken));
    }

    public async Task<Result<PlanDetailDto>> CreateAsync(SavePlanRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, null, cancellationToken);
        if (!validation.IsSuccess) return Result<PlanDetailDto>.Failure(validation.Error!);

        var values = validation.Value!;
        var now = clock.UtcNow;
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Code = values.Code,
            Name = values.Name,
            Description = values.Description,
            MonthlyPrice = values.MonthlyPrice,
            Currency = values.Currency,
            Status = values.Status,
            CreatedAt = now,
            UpdatedAt = now,
            Permissions = values.PermissionCodes.Select(code => new PlanPermission { PermissionCode = code }).ToList(),
            PriceHistory =
            [
                new PlanPriceHistory
                {
                    Id = Guid.NewGuid(),
                    MonthlyPrice = values.MonthlyPrice,
                    Currency = values.Currency,
                    EffectiveFrom = now
                }
            ]
        };
        await repository.AddAsync(plan, cancellationToken);
        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, "Plan creado", "PLAN", plan.Id.ToString(), plan.Name,
            $"Se creó el plan {plan.Code} con {plan.Permissions.Count} permisos."), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PlanDetailDto>.Success(await MapDetailAsync(plan, cancellationToken));
    }

    public async Task<Result<PlanDetailDto>> UpdateAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetAsync(id, cancellationToken);
        if (plan is null) return Result<PlanDetailDto>.Failure(AdminErrors.PlanNotFound);

        var validation = await ValidateAsync(request, id, cancellationToken);
        if (!validation.IsSuccess) return Result<PlanDetailDto>.Failure(validation.Error!);
        var values = validation.Value!;
        var now = clock.UtcNow;
        if (values.Status == PlanStatuses.Archived && await repository.CountAssignmentsAsync(id, cancellationToken) > 0)
            return Result<PlanDetailDto>.Failure(AdminErrors.PlanInUse);

        if (plan.MonthlyPrice != values.MonthlyPrice || !string.Equals(plan.Currency, values.Currency, StringComparison.Ordinal))
        {
            var currentPrice = plan.PriceHistory.SingleOrDefault(item => item.EffectiveUntil is null);
            if (currentPrice is not null) currentPrice.EffectiveUntil = now;
            plan.PriceHistory.Add(new PlanPriceHistory
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                MonthlyPrice = values.MonthlyPrice,
                Currency = values.Currency,
                EffectiveFrom = now
            });
        }

        plan.Code = values.Code;
        plan.Name = values.Name;
        plan.Description = values.Description;
        plan.MonthlyPrice = values.MonthlyPrice;
        plan.Currency = values.Currency;
        plan.Status = values.Status;
        plan.UpdatedAt = now;
        plan.Permissions.Clear();
        foreach (var code in values.PermissionCodes)
            plan.Permissions.Add(new PlanPermission { PlanId = plan.Id, PermissionCode = code });

        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, "Plan actualizado", "PLAN", plan.Id.ToString(), plan.Name,
            $"Se actualizó el plan {plan.Code}; ahora incluye {plan.Permissions.Count} permisos."), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var assignments = await repository.GetAssignmentsForPlanAsync(plan.Id, cancellationToken);
        foreach (var assignment in assignments) assignment.SyncStatus = PermissionSyncStatuses.Pending;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var assignment in assignments) await TrySynchronizeAsync(assignment, cancellationToken);

        return Result<PlanDetailDto>.Success(await MapDetailAsync(plan, cancellationToken));
    }

    public async Task<Result<PlanDetailDto>> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var plan = await repository.GetAsync(id, cancellationToken);
        if (plan is null) return Result<PlanDetailDto>.Failure(AdminErrors.PlanNotFound);
        var normalized = status.Trim().ToUpperInvariant();
        if (!PlanStatuses.All.Contains(normalized)) return Result<PlanDetailDto>.Failure(AdminErrors.Validation);
        if (normalized == PlanStatuses.Archived && await repository.CountAssignmentsAsync(id, cancellationToken) > 0)
            return Result<PlanDetailDto>.Failure(AdminErrors.PlanInUse);

        plan.Status = normalized;
        plan.UpdatedAt = clock.UtcNow;
        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, "Estado de plan actualizado", "PLAN", plan.Id.ToString(), plan.Name,
            $"El plan cambió al estado {normalized}."), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PlanDetailDto>.Success(await MapDetailAsync(plan, cancellationToken));
    }

    public async Task<Result<PlanAssignmentResultDto>> AssignAsync(Guid organizationId, AssignPlanRequest request, CancellationToken cancellationToken)
    {
        if (!await operationalPort.OrganizationExistsAsync(organizationId, cancellationToken))
            return Result<PlanAssignmentResultDto>.Failure(AdminErrors.OrganizationNotFound);
        var plan = await repository.GetAsync(request.PlanId, cancellationToken);
        if (plan is null)
            return Result<PlanAssignmentResultDto>.Failure(AdminErrors.PlanNotFound);
        if (plan.Status != PlanStatuses.Active)
            return Result<PlanAssignmentResultDto>.Failure(AdminErrors.PlanNotActive);

        var now = clock.UtcNow;
        var assignment = await repository.GetAssignmentAsync(organizationId, cancellationToken);
        if (assignment is null)
        {
            assignment = new TenantPlanAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = organizationId,
                PlanId = plan.Id,
                Plan = plan,
                AssignedAt = now,
                UpdatedAt = now,
                SyncStatus = PermissionSyncStatuses.Pending
            };
            await repository.AddAssignmentAsync(assignment, cancellationToken);
        }
        else
        {
            assignment.PlanId = plan.Id;
            assignment.Plan = plan;
            assignment.AssignedAt = now;
            assignment.UpdatedAt = now;
            assignment.SyncStatus = PermissionSyncStatuses.Pending;
            assignment.LastSyncErrorCode = null;
            if (!request.PreserveOverrides) assignment.Overrides.Clear();
        }

        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, "Plan asignado", "ORGANIZATION", organizationId.ToString(), organizationId.ToString(),
            $"Se asignó el plan {plan.Code} a la organización."), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await SynchronizeResultAsync(assignment, cancellationToken);
    }

    public async Task<Result<PlanAssignmentResultDto>> RetrySyncAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var assignment = await repository.GetAssignmentAsync(organizationId, cancellationToken);
        if (assignment is null) return Result<PlanAssignmentResultDto>.Failure(AdminErrors.AssignmentNotFound);
        assignment.SyncStatus = PermissionSyncStatuses.Pending;
        assignment.UpdatedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await SynchronizeResultAsync(assignment, cancellationToken);
    }

    public async Task<Result<PlanAssignmentResultDto>> SetPermissionOverrideAsync(
        Guid organizationId,
        string permissionCode,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var normalizedCode = permissionCode.Trim().ToLowerInvariant();
        var catalog = await operationalPort.GetPermissionCatalogAsync(cancellationToken);
        if (!catalog.Any(item => item.Code == normalizedCode))
            return Result<PlanAssignmentResultDto>.Failure(AdminErrors.PermissionNotFound);
        if (!await operationalPort.OrganizationExistsAsync(organizationId, cancellationToken))
            return Result<PlanAssignmentResultDto>.Failure(AdminErrors.OrganizationNotFound);

        var now = clock.UtcNow;
        var assignment = await repository.GetAssignmentAsync(organizationId, cancellationToken);
        var initializeManualPermissions = assignment is null ||
            (assignment.PlanId is null && assignment.Overrides.Count == 0);
        if (assignment is null)
        {
            assignment = new TenantPlanAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = organizationId,
                AssignedAt = now,
                UpdatedAt = now,
                SyncStatus = PermissionSyncStatuses.Pending
            };
            await repository.AddAssignmentAsync(assignment, cancellationToken);
        }

        if (initializeManualPermissions)
        {
            var currentPermissionCodes = await operationalPort.GetTenantPermissionCodesAsync(
                organizationId,
                cancellationToken);
            foreach (var currentCode in currentPermissionCodes.Distinct(StringComparer.Ordinal))
                assignment.Overrides.Add(new TenantPermissionOverride
                {
                    AssignmentId = assignment.Id,
                    PermissionCode = currentCode,
                    IsEnabled = true,
                    UpdatedAt = now
                });
        }

        var existing = assignment.Overrides.SingleOrDefault(item => item.PermissionCode == normalizedCode);
        if (existing is null)
            assignment.Overrides.Add(new TenantPermissionOverride
            {
                AssignmentId = assignment.Id,
                PermissionCode = normalizedCode,
                IsEnabled = enabled,
                UpdatedAt = now
            });
        else
        {
            existing.IsEnabled = enabled;
            existing.UpdatedAt = now;
        }

        assignment.SyncStatus = PermissionSyncStatuses.Pending;
        assignment.UpdatedAt = now;
        assignment.LastSyncErrorCode = null;
        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, "Permiso de organización actualizado", "ORGANIZATION",
            organizationId.ToString(), organizationId.ToString(), $"El permiso {normalizedCode} quedó {(enabled ? "habilitado" : "deshabilitado")}."), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await SynchronizeResultAsync(assignment, cancellationToken);
    }

    private async Task<Result<ValidatedPlan>> ValidateAsync(SavePlanRequest request, Guid? excludedId, CancellationToken cancellationToken)
    {
        var code = ApplicationHelpers.NormalizeCode(request.Code);
        var name = request.Name.Trim();
        var currency = request.Currency.Trim().ToUpperInvariant();
        var status = request.Status.Trim().ToUpperInvariant();
        var permissionCodes = request.PermissionCodes.Select(item => item.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order().ToArray();
        if (!ApplicationHelpers.IsValidPlanCode(code) || string.IsNullOrWhiteSpace(name) || name.Length > 120
            || request.Description.Trim().Length > 1000 || request.MonthlyPrice < 0 || currency.Length != 3
            || !currency.All(char.IsAsciiLetter)
            || !PlanStatuses.All.Contains(status))
            return Result<ValidatedPlan>.Failure(AdminErrors.Validation);
        if (await repository.CodeExistsAsync(code, excludedId, cancellationToken))
            return Result<ValidatedPlan>.Failure(AdminErrors.PlanCodeExists);

        var catalog = await operationalPort.GetPermissionCatalogAsync(cancellationToken);
        var existingCodes = catalog.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        if (permissionCodes.Any(codeValue => !existingCodes.Contains(codeValue)))
            return Result<ValidatedPlan>.Failure(AdminErrors.PermissionNotFound);
        return Result<ValidatedPlan>.Success(new ValidatedPlan(code, name, request.Description.Trim(), request.MonthlyPrice, currency, status, permissionCodes));
    }

    private async Task<Result<PlanAssignmentResultDto>> SynchronizeResultAsync(TenantPlanAssignment assignment, CancellationToken cancellationToken)
    {
        var synchronized = await TrySynchronizeAsync(assignment, cancellationToken);
        return synchronized
            ? Result<PlanAssignmentResultDto>.Success(MapAssignment(assignment))
            : Result<PlanAssignmentResultDto>.Failure(AdminErrors.PermissionSyncFailed);
    }

    private async Task<bool> TrySynchronizeAsync(TenantPlanAssignment assignment, CancellationToken cancellationToken)
    {
        var effectiveCodes = (assignment.Plan?.Permissions ?? [])
            .Select(item => item.PermissionCode)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in assignment.Overrides)
        {
            if (item.IsEnabled) effectiveCodes.Add(item.PermissionCode);
            else effectiveCodes.Remove(item.PermissionCode);
        }

        try
        {
            await operationalPort.ReplaceTenantPermissionsAsync(assignment.TenantId, effectiveCodes.Order().ToArray(), cancellationToken);
            assignment.SyncStatus = PermissionSyncStatuses.Synced;
            assignment.LastSyncErrorCode = null;
            assignment.LastSyncedAt = clock.UtcNow;
            assignment.UpdatedAt = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            assignment.SyncStatus = PermissionSyncStatuses.Failed;
            assignment.LastSyncErrorCode = AdminErrors.PermissionSyncFailed.Code;
            assignment.UpdatedAt = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    private async Task<PlanDetailDto> MapDetailAsync(Plan plan, CancellationToken cancellationToken)
        => new(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.MonthlyPrice,
            plan.Currency,
            plan.Status,
            plan.Permissions.Select(item => item.PermissionCode).Order().ToArray(),
            plan.PriceHistory.OrderByDescending(item => item.EffectiveFrom).Select(item => new PlanPriceHistoryDto(
                item.Id, item.MonthlyPrice, item.Currency, item.EffectiveFrom, item.EffectiveUntil)).ToArray(),
            await repository.CountAssignmentsAsync(plan.Id, cancellationToken));

    private static PlanAssignmentResultDto MapAssignment(TenantPlanAssignment assignment)
        => new(assignment.TenantId, assignment.PlanId, assignment.SyncStatus, assignment.LastSyncedAt);

    private sealed record ValidatedPlan(
        string Code,
        string Name,
        string Description,
        decimal MonthlyPrice,
        string Currency,
        string Status,
        IReadOnlyCollection<string> PermissionCodes);
}
