namespace SaviaUp.Admin.Application.Options;

public sealed class AdminJwtOptions
{
    public const string SectionName = "AdminJwt";
    public string Issuer { get; set; } = "saviaup.backend.admin";
    public string Audience { get; set; } = "saviaup.frontend.admin";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 30;
}

public sealed class OperationalFrontendOptions
{
    public const string SectionName = "OperationalFrontend";
    public string BaseUrl { get; set; } = "http://localhost:4200";
}

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string Mode { get; set; } = "Development";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "SaviaUp";
}
