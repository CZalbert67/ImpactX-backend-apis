using System.Security.Cryptography;

namespace ImpactX.Core.Identity;

public static class UsernamePolicy
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var candidate = input.Trim();
        if (candidate.Length is < MinLength or > MaxLength)
            return null;

        foreach (var c in candidate)
        {
            var valid = c is >= 'a' and <= 'z'
                        or >= '0' and <= '9'
                        or '.'
                        or '_'
                        or >= 'A' and <= 'Z';
            if (!valid)
                return null;
        }

        var lower = candidate.ToLowerInvariant();

        if (lower.Contains("..", StringComparison.Ordinal))
            return null;

        if (!IsAlphanumeric(lower[0]) || !IsAlphanumeric(lower[^1]))
            return null;

        return lower;
    }

    public static bool TryParse(string? input, out string? username)
    {
        username = Normalize(input);
        return username is not null;
    }

    public static string Generate(string nombre)
    {
        var baseName = SanitizeBase(nombre);
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var suffix = string.Create(4, bytes, static (span, b) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[b[i] % alphabet.Length];
            }
        });

        return string.Concat(baseName, "_", suffix);
    }

    private static string SanitizeBase(string? nombre)
    {
        var lower = (nombre ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0)
            return "usuario";

        var builder = new System.Text.StringBuilder();
        var lastWasUnderscore = false;
        foreach (var c in lower)
        {
            if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9')
            {
                builder.Append(c);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                lastWasUnderscore = true;
            }
        }

        var result = builder.ToString().TrimEnd('_');
        if (result.Length == 0)
            result = "usuario";

        var max = Math.Min(result.Length, MaxLength - 5);
        result = result[..max];
        return result;
    }

    private static bool IsAlphanumeric(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9';
}
