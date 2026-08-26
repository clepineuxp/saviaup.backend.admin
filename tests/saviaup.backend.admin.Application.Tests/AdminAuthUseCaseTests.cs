using Moq;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Application.UseCases;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.Tests;

public sealed class AdminAuthUseCaseTests
{
    [Fact]
    public async Task Login_does_not_reveal_if_email_or_password_failed()
    {
        var repository = new Mock<IAdminIdentityRepository>();
        repository.Setup(item => item.GetByNormalizedEmailAsync("MISSING@SAVIAUP.CO", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminUser?)null);
        var useCase = new AdminAuthUseCase(repository.Object, Mock.Of<IAdminPasswordHasher>(), Mock.Of<IAdminTokenIssuer>(),
            Mock.Of<IDateTimeProvider>(), Mock.Of<IAdminUnitOfWork>());

        var result = await useCase.LoginAsync(new LoginRequest("missing@saviaup.co", "wrong"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminErrors.InvalidCredentials.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Login_issues_admin_token_for_active_account()
    {
        var user = new AdminUser { Id = Guid.NewGuid(), Email = "admin@saviaup.co", NormalizedEmail = "ADMIN@SAVIAUP.CO", Name = "Admin", PasswordHash = "hash", IsActive = true };
        var repository = new Mock<IAdminIdentityRepository>();
        repository.Setup(item => item.GetByNormalizedEmailAsync(user.NormalizedEmail, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var hasher = new Mock<IAdminPasswordHasher>();
        hasher.Setup(item => item.Verify(user, user.PasswordHash, "Valid1234!xx")).Returns(true);
        var issuer = new Mock<IAdminTokenIssuer>();
        var expiration = new DateTimeOffset(2026, 8, 26, 15, 0, 0, TimeSpan.Zero);
        issuer.Setup(item => item.Issue(user)).Returns(("signed-token", expiration));
        var unitOfWork = new Mock<IAdminUnitOfWork>();
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var useCase = new AdminAuthUseCase(repository.Object, hasher.Object, issuer.Object,
            Mock.Of<IDateTimeProvider>(item => item.UtcNow == expiration.AddMinutes(-30)), unitOfWork.Object);

        var result = await useCase.LoginAsync(new LoginRequest(user.Email, "Valid1234!xx"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("signed-token", result.Value?.AccessToken);
        Assert.Equal(expiration, result.Value?.ExpiresAt);
        Assert.NotNull(user.LastLoginAt);
    }
}
