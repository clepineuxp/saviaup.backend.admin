using SaviaUp.Admin.Application.Common;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Application.UseCases;

public sealed class OrganizationAdministrationUseCase(
    IOperationalAdminPort operationalPort,
    IAdminOverviewUseCase overview,
    ISecureTokenGenerator tokenGenerator,
    IPasswordResetLinkFactory linkFactory,
    IAdminEmailSender emailSender,
    IAuditRepository auditRepository,
    IAdminActorContext actor,
    IDateTimeProvider clock,
    IAdminUnitOfWork unitOfWork) : IOrganizationAdministrationUseCase
{
    public async Task<Result<OrganizationSummaryDto>> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!await operationalPort.SetOrganizationStatusAsync(id, isActive, cancellationToken))
            return Result<OrganizationSummaryDto>.Failure(AdminErrors.OrganizationNotFound);
        await AuditAsync(isActive ? "Organización activada" : "Organización desactivada", "ORGANIZATION", id, id.ToString(),
            $"El estado de la organización cambió a {(isActive ? "activo" : "inactivo")}.", cancellationToken);
        var organizations = await overview.GetOrganizationsAsync(cancellationToken);
        if (!organizations.IsSuccess) return Result<OrganizationSummaryDto>.Failure(organizations.Error!);
        var organization = organizations.Value!.SingleOrDefault(item => item.Id == id);
        return organization is null
            ? Result<OrganizationSummaryDto>.Failure(AdminErrors.OrganizationNotFound)
            : Result<OrganizationSummaryDto>.Success(organization);
    }

    public async Task<Result<OrganizationDetailDto>> ChangeOwnerAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var changed = await operationalPort.ChangeOwnerAsync(id, userId, cancellationToken);
        if (!changed.IsSuccess) return Result<OrganizationDetailDto>.Failure(changed.Error!);
        await AuditAsync("Owner actualizado", "ORGANIZATION", id, id.ToString(), $"La propiedad fue transferida al usuario {userId}.", cancellationToken, "SECURITY");
        return await overview.GetOrganizationAsync(id, cancellationToken);
    }

    public async Task<Result<PasswordResetResultDto>> RequestPasswordResetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var recipient = await operationalPort.GetPasswordResetRecipientAsync(userId, cancellationToken);
        if (recipient is null) return Result<PasswordResetResultDto>.Failure(AdminErrors.PlatformUserNotFound);
        var rawToken = tokenGenerator.Generate();
        var now = clock.UtcNow;
        await operationalPort.AddPasswordResetTokenAsync(userId, tokenGenerator.Hash(rawToken), now, now.AddHours(1), cancellationToken);
        await emailSender.SendPlatformPasswordResetAsync(recipient.Value.Email, recipient.Value.PreferredLanguage, linkFactory.Create(rawToken), cancellationToken);
        await AuditAsync("Restablecimiento solicitado", "PLATFORM_USER", userId, MaskEmail(recipient.Value.Email),
            "Se generó y entregó un enlace de restablecimiento de contraseña.", cancellationToken, "SECURITY");
        return Result<PasswordResetResultDto>.Success(new PasswordResetResultDto(MaskEmail(recipient.Value.Email), emailSender.DeliveryMode));
    }

    public async Task<Result<PlatformUserDto>> SetMembershipStatusAsync(Guid membershipId, bool isActive, CancellationToken cancellationToken)
    {
        var changed = await operationalPort.SetMembershipStatusAsync(membershipId, isActive, cancellationToken);
        if (!changed.IsSuccess) return Result<PlatformUserDto>.Failure(changed.Error!);
        await AuditAsync(isActive ? "Acceso reactivado" : "Acceso deshabilitado", "MEMBERSHIP", membershipId, membershipId.ToString(),
            $"La membresía quedó {(isActive ? "activa" : "inactiva")}.", cancellationToken, "USER");
        var user = await operationalPort.GetUserAsync(changed.Value!, cancellationToken);
        return user is null ? Result<PlatformUserDto>.Failure(AdminErrors.PlatformUserNotFound) : Result<PlatformUserDto>.Success(user);
    }

    public async Task<Result<PlatformUserDto>> ReassignMembershipAsync(ReassignMembershipRequest request, CancellationToken cancellationToken)
    {
        var reassigned = await operationalPort.ReassignMembershipAsync(request.UserId, request.MembershipId, request.TargetOrganizationId, cancellationToken);
        if (!reassigned.IsSuccess) return Result<PlatformUserDto>.Failure(reassigned.Error!);
        await AuditAsync("Membresía reasignada", "MEMBERSHIP", request.MembershipId, request.MembershipId.ToString(),
            $"El usuario {request.UserId} fue reasignado a la organización {request.TargetOrganizationId}.", cancellationToken, "USER");
        var user = await operationalPort.GetUserAsync(reassigned.Value!, cancellationToken);
        return user is null ? Result<PlatformUserDto>.Failure(AdminErrors.PlatformUserNotFound) : Result<PlatformUserDto>.Success(user);
    }

    private async Task AuditAsync(string action, string subjectType, Guid subjectId, string subjectName, string detail, CancellationToken cancellationToken, string kind = "ORGANIZATION")
    {
        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, action, subjectType, subjectId.ToString(), subjectName, detail, kind), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string MaskEmail(string email)
    {
        var separator = email.LastIndexOf('@');
        if (separator <= 0) return "***";
        var local = email[..separator];
        return $"{local[..Math.Min(2, local.Length)]}{new string('*', Math.Max(2, local.Length - 2))}{email[separator..]}";
    }
}
