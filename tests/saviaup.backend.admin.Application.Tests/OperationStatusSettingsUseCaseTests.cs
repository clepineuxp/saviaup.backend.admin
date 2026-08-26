using Moq;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Application.UseCases;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.Tests;

public sealed class OperationStatusSettingsUseCaseTests
{
    [Fact]
    public async Task Get_returns_safe_defaults_when_settings_are_not_persisted()
    {
        var fixture = new Fixture();

        var result = await fixture.Create().GetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(120, result.Value?.InactivityThresholdMinutes);
        Assert.Equal(OperationIssueSeverities.Critical, result.Value?.InactivitySeverity);
        Assert.Equal(OperationIssueSeverities.Warning, result.Value?.CashRegisterSeverity);
    }

    [Fact]
    public async Task Update_persists_every_operational_rule_parameter()
    {
        var fixture = new Fixture();
        OperationStatusSettings? added = null;
        fixture.Repository.Setup(item => item.AddAsync(It.IsAny<OperationStatusSettings>(), It.IsAny<CancellationToken>()))
            .Callback<OperationStatusSettings, CancellationToken>((value, _) => added = value)
            .Returns(Task.CompletedTask);

        var result = await fixture.Create().UpdateAsync(new UpdateOperationStatusSettingsRequest(
            false,
            45,
            "warning",
            true,
            "critical"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.False(added.InactivityRuleEnabled);
        Assert.Equal(45, added.InactivityThresholdMinutes);
        Assert.Equal(OperationIssueSeverities.Warning, added.InactivitySeverity);
        Assert.Equal(OperationIssueSeverities.Critical, added.CashRegisterSeverity);
        fixture.UnitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Audits.Verify(item => item.AddAsync(
            It.Is<AdminAuditLog>(audit => audit.Action == "Reglas operacionales actualizadas"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, "WARNING", "WARNING")]
    [InlineData(10081, "WARNING", "WARNING")]
    [InlineData(120, "INFO", "WARNING")]
    [InlineData(120, "CRITICAL", "INFO")]
    public async Task Update_rejects_invalid_thresholds_and_severities(
        int minutes,
        string inactivitySeverity,
        string cashSeverity)
    {
        var fixture = new Fixture();

        var result = await fixture.Create().UpdateAsync(new UpdateOperationStatusSettingsRequest(
            true,
            minutes,
            inactivitySeverity,
            true,
            cashSeverity), CancellationToken.None);

        Assert.False(result.IsSuccess);
        fixture.UnitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Mock<IOperationStatusSettingsRepository> Repository { get; } = new();
        public Mock<IAuditRepository> Audits { get; } = new();
        public Mock<IAdminUnitOfWork> UnitOfWork { get; } = new();

        public Fixture()
        {
            Repository.Setup(item => item.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperationStatusSettings?)null);
            Audits.Setup(item => item.AddAsync(It.IsAny<AdminAuditLog>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        public OperationStatusSettingsUseCase Create() => new(
            Repository.Object,
            Audits.Object,
            Mock.Of<IAdminActorContext>(item =>
                item.UserId == Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") &&
                item.Email == "admin@saviaup.co"),
            Mock.Of<IDateTimeProvider>(item =>
                item.UtcNow == new DateTimeOffset(2026, 8, 26, 20, 0, 0, TimeSpan.Zero)),
            UnitOfWork.Object);
    }
}
