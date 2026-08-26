using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaviaUp.Admin.Application.Options;
using SaviaUp.Admin.Application.Ports;

namespace SaviaUp.Admin.Infrastructure.Email;

internal sealed class PasswordResetLinkFactory(IOptions<OperationalFrontendOptions> options) : IPasswordResetLinkFactory
{
    public string Create(string rawToken)
        => $"{options.Value.BaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
}

internal sealed class DevelopmentAdminEmailSender(ILogger<DevelopmentAdminEmailSender> logger) : IAdminEmailSender
{
    public string DeliveryMode => "DEVELOPMENT";

    public Task SendPlatformPasswordResetAsync(string email, string language, string resetLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development password recovery suppressed for {EmailDomain} ({Language}); token and link were not logged.", Domain(email), language);
        return Task.CompletedTask;
    }

    private static string Domain(string email)
    {
        var separator = email.LastIndexOf('@');
        return separator < 0 ? "unknown" : email[separator..];
    }
}

internal sealed class SmtpAdminEmailSender(IOptions<EmailOptions> options) : IAdminEmailSender
{
    private readonly EmailOptions _options = options.Value;
    public string DeliveryMode => "EMAIL";

    public async Task SendPlatformPasswordResetAsync(string email, string language, string resetLink, CancellationToken cancellationToken)
    {
        var spanish = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = spanish ? "Restablece tu contraseña de SaviaUp" : "Reset your SaviaUp password",
            Body = spanish
                ? $"Un administrador solicitó restablecer tu contraseña. Usa este enlace durante la próxima hora: {resetLink}"
                : $"An administrator requested a password reset. Use this link within the next hour: {resetLink}",
            IsBodyHtml = false
        };
        message.To.Add(email);
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Username, _options.Password)
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
