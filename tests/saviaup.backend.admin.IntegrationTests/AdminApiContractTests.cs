using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaviaUp.Admin.Application.Contracts;
using SaviaUp.Admin.Application.UseCases;
using SaviaUp.Admin.Domain.Common;

namespace SaviaUp.Admin.IntegrationTests;

public sealed class AdminApiContractTests : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client;

    public AdminApiContractTests(AdminApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Login_returns_frontend_session_contract()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest("admin@saviaup.local", "Valid1234!xx"));

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<AdminSessionDto>();
        Assert.NotNull(session);
        Assert.Equal("integration-token", session.AccessToken);
        Assert.Equal("admin@saviaup.local", session.Administrator.Email);
    }

    [Fact]
    public async Task Protected_endpoint_returns_stable_unauthenticated_error()
    {
        var response = await _client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("ADMIN_UNAUTHENTICATED", json, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("AdminJwt:SigningKey", "integration-tests-only-signing-key-with-32-bytes");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAdminAuthUseCase>();
            services.AddScoped<IAdminAuthUseCase, StubAuthUseCase>();
        });
    }
}

internal sealed class StubAuthUseCase : IAdminAuthUseCase
{
    public Task<Result<AdminSessionDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Result<AdminSessionDto>.Success(new AdminSessionDto(
            new AdminIdentityDto(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Integration Admin", request.Email, "SUPER_ADMIN", true, null),
            "integration-token",
            DateTimeOffset.UtcNow.AddMinutes(30))));
}
