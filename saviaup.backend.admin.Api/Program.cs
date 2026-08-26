using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SaviaUp.Admin.Api.Http;
using SaviaUp.Admin.Application.Options;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Application.UseCases;
using SaviaUp.Admin.Domain.Common;
using SaviaUp.Admin.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var jwtOptions = builder.Configuration.GetSection(AdminJwtOptions.SectionName).Get<AdminJwtOptions>() ?? new AdminJwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    throw new InvalidOperationException("AdminJwt:SigningKey must be configured with at least 32 bytes.");

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminActorContext, AdminActorContext>();
builder.Services.AddScoped<IAdminAuthUseCase, AdminAuthUseCase>();
builder.Services.AddScoped<IAdminUsersUseCase, AdminUsersUseCase>();
builder.Services.AddScoped<IPlansUseCase, PlansUseCase>();
builder.Services.AddScoped<IAdminOverviewUseCase, AdminOverviewUseCase>();
builder.Services.AddScoped<IOrganizationAdministrationUseCase, OrganizationAdministrationUseCase>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = "admin_role",
        NameClaimType = "email"
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ErrorEnvelope.From(AdminErrors.Unauthenticated));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ErrorEnvelope.From(AdminErrors.Forbidden));
        }
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminAccess", policy => policy.RequireRole("SUPER_ADMIN", "SUPPORT"))
    .AddPolicy("SuperAdmin", policy => policy.RequireRole("SUPER_ADMIN"));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4300"];
builder.Services.AddCors(options => options.AddPolicy("AdminFrontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ErrorEnvelope.From(new Error("ADMIN_RATE_LIMITED", "Demasiados intentos. Intenta nuevamente más tarde.", 429)),
            cancellationToken);
    };
    options.AddPolicy("admin-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SaviaUp Admin API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AdminFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<AdminAccountValidationMiddleware>();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions());
app.Run();

public partial class Program;
