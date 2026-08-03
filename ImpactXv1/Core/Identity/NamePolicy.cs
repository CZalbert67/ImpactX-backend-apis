using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ImpactX.Core.Exceptions;

namespace ImpactX.Core.Identity;

/// <summary>
/// Política de nombres visibles: normaliza el texto y bloquea palabras
/// altisonantes (insultos y lenguaje ofensivo en español).
/// </summary>
public static partial class NamePolicy
{
    private static readonly HashSet<string> OffensiveWords = new(StringComparer.Ordinal)
    {
        "cabron", "cojones", "concha", "culo", "estupido", "gilipollas",
        "hijueputa", "hpta", "idiota", "imbecil", "joder", "maldito", "maricon",
        "mierda", "pendejo", "pinche", "polla", "puta", "puto", "verga"
    };

    private static readonly Regex WordSeparators = CreateWordSeparatorsRegex();

    [GeneratedRegex(@"[\s\-_.]+", RegexOptions.Compiled)]
    private static partial Regex CreateWordSeparatorsRegex();

    public static bool ContainsOffensiveWord(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return false;
        }

        return WordSeparators
            .Split(nombre)
            .Select(NormalizeToken)
            .Any(token => token.Length > 0 && OffensiveWords.Contains(token));
    }

    public static string Normalize(string nombre)
    {
        return string.Join(' ', nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static void Validate(string? nombre)
    {
        if (ContainsOffensiveWord(nombre))
        {
            throw new BadRequestException("El nombre contiene palabras inapropiadas.");
        }
    }

    private static string NormalizeToken(string token)
    {
        var lowered = token.ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        foreach (var character in lowered)
        {
            var normalized = RemoveDiacritics(character);
            if (char.IsLetterOrDigit(normalized))
            {
                builder.Append(normalized);
            }
        }

        return builder.ToString();
    }

    private static char RemoveDiacritics(char character)
    {
        var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
        foreach (var part in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(part);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                return part;
            }
        }

        return character;
    }
}
