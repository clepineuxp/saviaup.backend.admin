using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.UseCases;

public sealed class OperationStatusSettingsUseCase(
    IOperationStatusSettingsRepository repository,
    IAuditRepository auditRepository,
    IAdminActorContext actor,
    IDateTimeProvider clock,
    IAdminUnitOfWork unitOfWork) : IOperationStatusSettingsUseCase
{
    public async Task<Result<OperationStatusSettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken) ?? DefaultSettings();
        return Result<OperationStatusSettingsDto>.Success(Map(settings));
    }

    public async Task<Result<OperationStatusSettingsDto>> UpdateAsync(
        UpdateOperationStatusSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InactivitySeverity) ||
            string.IsNullOrWhiteSpace(request.CashRegisterSeverity) ||
            request.InactivityThresholdMinutes is < 1 or > 10080)
            return Result<OperationStatusSettingsDto>.Failure(AdminErrors.Validation);

        var inactivitySeverity = request.InactivitySeverity.Trim().ToUpperInvariant();
        var cashRegisterSeverity = request.CashRegisterSeverity.Trim().ToUpperInvariant();
        if (
            !OperationIssueSeverities.All.Contains(inactivitySeverity) ||
            !OperationIssueSeverities.All.Contains(cashRegisterSeverity))
            return Result<OperationStatusSettingsDto>.Failure(AdminErrors.Validation);

        var settings = await repository.GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = DefaultSettings();
            await repository.AddAsync(settings, cancellationToken);
        }

        settings.InactivityRuleEnabled = request.InactivityRuleEnabled;
        settings.InactivityThresholdMinutes = request.InactivityThresholdMinutes;
        settings.InactivitySeverity = inactivitySeverity;
        settings.CashRegisterRuleEnabled = request.CashRegisterRuleEnabled;
        settings.CashRegisterSeverity = cashRegisterSeverity;
        settings.UpdatedAt = clock.UtcNow;
        settings.UpdatedByAdminUserId = actor.UserId;

        await auditRepository.AddAsync(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            ActorAdminUserId = actor.UserId,
            ActorEmail = actor.Email,
            Action = "Reglas operacionales actualizadas",
            SubjectType = "OPERATION_SETTINGS",
            SubjectId = settings.Id.ToString(),
            SubjectName = "Estados operacionales",
            Detail = $"Inactividad: {settings.InactivityThresholdMinutes} minutos/{settings.InactivitySeverity}; caja: {settings.CashRegisterSeverity}.",
            Kind = "OPERATION",
            CorrelationId = actor.CorrelationId,
            OccurredAt = clock.UtcNow
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<OperationStatusSettingsDto>.Success(Map(settings));
    }

    internal static OperationalStatusPolicy ToPolicy(OperationStatusSettings? settings)
    {
        settings ??= DefaultSettings();
        return new OperationalStatusPolicy(
            settings.InactivityRuleEnabled,
            settings.InactivityThresholdMinutes,
            settings.InactivitySeverity,
            settings.CashRegisterRuleEnabled,
            settings.CashRegisterSeverity);
    }

    private static OperationStatusSettings DefaultSettings() => new()
    {
        UpdatedAt = DateTimeOffset.MinValue
    };

    private static OperationStatusSettingsDto Map(OperationStatusSettings settings) => new(
        settings.InactivityRuleEnabled,
        settings.InactivityThresholdMinutes,
        settings.InactivitySeverity,
        settings.CashRegisterRuleEnabled,
        settings.CashRegisterSeverity,
        settings.UpdatedAt == DateTimeOffset.MinValue ? null : settings.UpdatedAt);
}
