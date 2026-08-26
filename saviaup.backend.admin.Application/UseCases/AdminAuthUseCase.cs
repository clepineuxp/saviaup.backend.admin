using SaviaUp.Admin.Application.Common;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.Application.UseCases;

public sealed class AdminAuthUseCase(
    IAdminIdentityRepository repository,
    IAdminPasswordHasher passwordHasher,
    IAdminTokenIssuer tokenIssuer,
    IDateTimeProvider clock,
    IAdminUnitOfWork unitOfWork) : IAdminAuthUseCase
{
    public async Task<Result<AdminSessionDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AdminSessionDto>.Failure(AdminErrors.InvalidCredentials);

        var user = await repository.GetByNormalizedEmailAsync(ApplicationHelpers.NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(user, user.PasswordHash, request.Password))
            return Result<AdminSessionDto>.Failure(AdminErrors.InvalidCredentials);

        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var issued = tokenIssuer.Issue(user);
        return Result<AdminSessionDto>.Success(new AdminSessionDto(
            new AdminIdentityDto(user.Id, user.Name, user.Email, user.RoleCode, user.IsActive, user.LastLoginAt),
            issued.Token,
            issued.ExpiresAt));
    }
}
