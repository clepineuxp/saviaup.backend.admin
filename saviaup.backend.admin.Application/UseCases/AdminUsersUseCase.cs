using SaviaUp.Admin.Application.Common;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;
using System.Net.Mail;

namespace SaviaUp.Admin.Application.UseCases;

public sealed class AdminUsersUseCase(
    IAdminIdentityRepository repository,
    IAdminPasswordHasher passwordHasher,
    IAuditRepository auditRepository,
    IAdminActorContext actor,
    IDateTimeProvider clock,
    IAdminUnitOfWork unitOfWork) : IAdminUsersUseCase
{
    public async Task<Result<IReadOnlyCollection<AdminIdentityDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await repository.ListAsync(cancellationToken);
        return Result<IReadOnlyCollection<AdminIdentityDto>>.Success(users.Select(Map).ToArray());
    }

    public async Task<Result<AdminIdentityDto>> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var role = request.RoleCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email)
            || !MailAddress.TryCreate(request.Email.Trim(), out _) || !StrongPassword(request.Password) || !AdminRoleCodes.All.Contains(role))
            return Result<AdminIdentityDto>.Failure(AdminErrors.Validation);

        var normalizedEmail = ApplicationHelpers.NormalizeEmail(request.Email);
        if (await repository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
            return Result<AdminIdentityDto>.Failure(AdminErrors.AdminEmailExists);

        var now = clock.UtcNow;
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            RoleCode = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.Hash(user, request.Password);
        await repository.AddAsync(user, cancellationToken);
        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, "Administrador creado", "ADMIN_USER", user.Id.ToString(), user.Name,
            $"Se creó el administrador {user.Email} con rol {user.RoleCode}.", "SECURITY"), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminIdentityDto>.Success(Map(user));
    }

    public async Task<Result<AdminIdentityDto>> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null) return Result<AdminIdentityDto>.Failure(AdminErrors.AdminUserNotFound);
        if (user.Id == actor.UserId && !isActive) return Result<AdminIdentityDto>.Failure(AdminErrors.Validation);

        user.IsActive = isActive;
        user.UpdatedAt = clock.UtcNow;
        await auditRepository.AddAsync(ApplicationHelpers.Audit(actor, clock, isActive ? "Administrador activado" : "Administrador desactivado",
            "ADMIN_USER", user.Id.ToString(), user.Name, $"Estado administrativo actualizado a {(isActive ? "activo" : "inactivo")}.", "SECURITY"), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminIdentityDto>.Success(Map(user));
    }

    private static AdminIdentityDto Map(AdminUser user)
        => new(user.Id, user.Name, user.Email, user.RoleCode, user.IsActive, user.LastLoginAt);

    private static bool StrongPassword(string password)
        => password.Length >= 12 && password.Any(char.IsUpper) && password.Any(char.IsLower)
            && password.Any(char.IsDigit) && password.Any(character => !char.IsLetterOrDigit(character));
}
