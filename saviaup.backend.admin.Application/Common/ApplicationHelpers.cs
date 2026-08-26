using System.Text;
using SaviaUp.Admin.Application.Ports;
using SaviaUp.Admin.Domain.Entities;

namespace SaviaUp.Admin.Application.Common;

public static class ApplicationHelpers
{
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant().Replace(' ', '_');

    public static bool IsValidPlanCode(string code)
        => code.Length is > 1 and <= 40 && code.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    public static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousDash = false;
        foreach (var character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousDash = false;
            }
            else if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static AdminAuditLog Audit(
        IAdminActorContext actor,
        IDateTimeProvider clock,
        string action,
        string subjectType,
        string subjectId,
        string subjectName,
        string detail,
        string kind = "ORGANIZATION") => new()
        {
            Id = Guid.NewGuid(),
            ActorAdminUserId = actor.UserId,
            ActorEmail = actor.Email,
            Action = action,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectName = subjectName,
            Detail = detail,
            Kind = kind,
            CorrelationId = actor.CorrelationId,
            OccurredAt = clock.UtcNow
        };
}
