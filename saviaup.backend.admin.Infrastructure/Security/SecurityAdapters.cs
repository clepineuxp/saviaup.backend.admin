using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SaviaUp.Admin.Application.Options;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Infrastructure.Security;

internal sealed class AdminPasswordHasher : IAdminPasswordHasher
{
    private readonly PasswordHasher<AdminUser> _hasher = new();

    public string Hash(AdminUser user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(AdminUser user, string passwordHash, string password)
        => _hasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
}

internal sealed class AdminTokenIssuer(IOptions<AdminJwtOptions> options, IDateTimeProvider clock) : IAdminTokenIssuer
{
    private readonly AdminJwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Issue(AdminUser user)
    {
        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
            throw new InvalidOperationException("AdminJwt:SigningKey must contain at least 32 bytes.");
        var expiresAt = clock.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.RoleCode),
                new Claim("admin_role", user.RoleCode)
            ],
            notBefore: clock.UtcNow.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
