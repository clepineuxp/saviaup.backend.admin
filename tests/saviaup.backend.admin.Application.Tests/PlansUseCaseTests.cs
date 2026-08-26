using Moq;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Application.UseCases;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.Tests;

public sealed class PlansUseCaseTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PlanId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_validates_permissions_and_records_initial_price()
    {
        var fixture = new Fixture();
        fixture.Operational.Setup(item => item.GetPermissionCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Catalog("tables.read", "products.read"));
        fixture.Plans.Setup(item => item.CodeExistsAsync("STANDARD", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Plan? added = null;
        fixture.Plans.Setup(item => item.AddAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Callback<Plan, CancellationToken>((value, _) => added = value).Returns(Task.CompletedTask);
        fixture.Plans.Setup(item => item.CountAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await fixture.Create().CreateAsync(new SavePlanRequest(
            "standard", "Standard", "Mesas y productos", 89000, "cop", "DRAFT", ["tables.read", "products.read"]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("STANDARD", added.Code);
        Assert.Equal(2, added.Permissions.Count);
        Assert.Single(added.PriceHistory);
        Assert.Equal(89000, added.PriceHistory.Single().MonthlyPrice);
        fixture.Audits.Verify(item => item.AddAsync(It.Is<AdminAuditLog>(audit => audit.Action == "Plan creado"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_materializes_only_effective_permissions_in_operational_database()
    {
        var fixture = new Fixture();
        var plan = PlanWith("tables.read", "products.read");
        fixture.Operational.Setup(item => item.OrganizationExistsAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fixture.Plans.Setup(item => item.GetAsync(PlanId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        fixture.Plans.Setup(item => item.GetAssignmentAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((TenantPlanAssignment?)null);
        TenantPlanAssignment? assignment = null;
        fixture.Plans.Setup(item => item.AddAssignmentAsync(It.IsAny<TenantPlanAssignment>(), It.IsAny<CancellationToken>()))
            .Callback<TenantPlanAssignment, CancellationToken>((value, _) => assignment = value).Returns(Task.CompletedTask);

        var result = await fixture.Create().AssignAsync(TenantId, new AssignPlanRequest(PlanId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PermissionSyncStatuses.Synced, assignment?.SyncStatus);
        fixture.Operational.Verify(item => item.ReplaceTenantPermissionsAsync(
            TenantId,
            It.Is<IReadOnlyCollection<string>>(codes => codes.SequenceEqual(new[] { "products.read", "tables.read" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failed_operational_sync_is_persisted_and_can_be_retried()
    {
        var fixture = new Fixture();
        var assignment = new TenantPlanAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            PlanId = PlanId,
            Plan = PlanWith("tables.read"),
            SyncStatus = PermissionSyncStatuses.Synced,
            Overrides = []
        };
        fixture.Operational.Setup(item => item.GetPermissionCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog("inventory.stock.read"));
        fixture.Operational.Setup(item => item.OrganizationExistsAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fixture.Plans.Setup(item => item.GetAssignmentAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(assignment);
        fixture.Operational.Setup(item => item.ReplaceTenantPermissionsAsync(TenantId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Create().SetPermissionOverrideAsync(TenantId, "inventory.stock.read", true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminErrors.PermissionSyncFailed.Code, result.Error?.Code);
        Assert.Equal(PermissionSyncStatuses.Failed, assignment.SyncStatus);
        Assert.Equal(AdminErrors.PermissionSyncFailed.Code, assignment.LastSyncErrorCode);
        fixture.UnitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Manual_permission_change_without_plan_preserves_other_current_permissions()
    {
        var fixture = new Fixture();
        fixture.Operational.Setup(item => item.GetPermissionCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Catalog("tables.read", "products.read"));
        fixture.Operational.Setup(item => item.OrganizationExistsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        fixture.Operational.Setup(item => item.GetTenantPermissionCodesAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["products.read", "tables.read"]);
        fixture.Plans.Setup(item => item.GetAssignmentAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPlanAssignment?)null);
        TenantPlanAssignment? assignment = null;
        fixture.Plans.Setup(item => item.AddAssignmentAsync(It.IsAny<TenantPlanAssignment>(), It.IsAny<CancellationToken>()))
            .Callback<TenantPlanAssignment, CancellationToken>((value, _) => assignment = value)
            .Returns(Task.CompletedTask);

        var result = await fixture.Create().SetPermissionOverrideAsync(
            TenantId,
            "tables.read",
            false,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(assignment);
        Assert.Null(assignment.PlanId);
        Assert.Equal(2, assignment.Overrides.Count);
        fixture.Operational.Verify(item => item.ReplaceTenantPermissionsAsync(
            TenantId,
            It.Is<IReadOnlyCollection<string>>(codes => codes.SequenceEqual(new[] { "products.read" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Plan_rejects_unknown_operational_permission()
    {
        var fixture = new Fixture();
        fixture.Operational.Setup(item => item.GetPermissionCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog("tables.read"));
        fixture.Plans.Setup(item => item.CodeExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await fixture.Create().CreateAsync(new SavePlanRequest(
            "STANDARD", "Standard", "", 0, "COP", "DRAFT", ["invented.permission"]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminErrors.PermissionNotFound.Code, result.Error?.Code);
        fixture.Plans.Verify(item => item.AddAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Plan PlanWith(params string[] permissions) => new()
    {
        Id = PlanId,
        Code = "STANDARD",
        Name = "Standard",
        Status = PlanStatuses.Active,
        Permissions = permissions.Select(code => new PlanPermission { PlanId = PlanId, PermissionCode = code }).ToList()
    };

    private static IReadOnlyCollection<OperationalPermission> Catalog(params string[] codes)
        => codes.Select(code => new OperationalPermission(Guid.NewGuid(), code, code, code.Split('.')[0], "Module", true)).ToArray();

    private sealed class Fixture
    {
        public Mock<IPlanRepository> Plans { get; } = new();
        public Mock<IOperationalAdminPort> Operational { get; } = new();
        public Mock<IAuditRepository> Audits { get; } = new();
        public Mock<IAdminUnitOfWork> UnitOfWork { get; } = new();

        public Fixture()
        {
            UnitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Audits.Setup(item => item.AddAsync(It.IsAny<AdminAuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Operational.Setup(item => item.ReplaceTenantPermissionsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public PlansUseCase Create() => new(
            Plans.Object,
            Operational.Object,
            Audits.Object,
            Mock.Of<IAdminActorContext>(item => item.UserId == Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") && item.Email == "admin@saviaup.co"),
            Mock.Of<IDateTimeProvider>(item => item.UtcNow == new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero)),
            UnitOfWork.Object);
    }
}
