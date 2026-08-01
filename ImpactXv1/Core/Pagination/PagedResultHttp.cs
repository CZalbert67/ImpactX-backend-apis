using Microsoft.AspNetCore.Http;

namespace ImpactX.Core.Pagination;

/// <summary>
/// Estrategia de compatibilidad HTTP V1: los endpoints legacy que conservan
/// cuerpo List&lt;T&gt; devuelven el siguiente token de continuación en el
/// header X-Continuation-Token. El token nunca viaja en la URL ni en el body
/// de endpoints legacy, y nunca se devuelve cuando no hay más páginas.
/// </summary>
public static class PagedResultHttp
{
    public const string ContinuationHeader = "X-Continuation-Token";

    public static void ApplyContinuationToken<T>(HttpResponse response, PagedResult<T> page)
        => ApplyContinuationToken(response, page.ContinuationToken);

    public static void ApplyContinuationToken(HttpResponse response, string? continuationToken)
    {
        if (!string.IsNullOrEmpty(continuationToken))
        {
            response.Headers[ContinuationHeader] = continuationToken;
        }
    }
}
