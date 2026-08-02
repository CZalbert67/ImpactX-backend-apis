using ImpactX.Core.Exceptions;

namespace ImpactX.Core.Pagination;

/// <summary>
/// Validación centralizada de parámetros de paginación (pageSize y
/// continuationToken). Reglas:
/// - pageSize nulo → default 20; mínimo 1; máximo 100 (fuera de rango → 400).
/// - continuationToken nulo → permitido (primera página).
/// - token vacío, con caracteres de control (CR/LF) o excesivamente largo → 400.
/// - El token es opaco: nunca se incluye en errores ni en logs.
/// </summary>
public static class PaginationValidator
{
    public static int ResolvePageSize(int? pageSize)
    {
        if (pageSize is null)
            return PaginationDefaults.DefaultPageSize;

        if (pageSize < PaginationDefaults.MinPageSize ||
            pageSize > PaginationDefaults.MaxPageSize)
        {
            throw new BadRequestException(
                $"pageSize debe estar entre {PaginationDefaults.MinPageSize} y {PaginationDefaults.MaxPageSize}.");
        }

        return pageSize.Value;
    }

    public static void ValidateContinuationToken(string? continuationToken)
    {
        if (continuationToken is null)
            return;

        if (string.IsNullOrWhiteSpace(continuationToken) ||
            continuationToken.Length > PaginationDefaults.MaxContinuationTokenLength ||
            ContainsControlCharacters(continuationToken))
        {
            throw new BadRequestException("El token de continuación es inválido o ha expirado.");
        }
    }

    public static int Resolve(int? pageSize, string? continuationToken)
    {
        var size = ResolvePageSize(pageSize);
        ValidateContinuationToken(continuationToken);
        return size;
    }

    private static bool ContainsControlCharacters(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
                return true;
        }
        return false;
    }
}
