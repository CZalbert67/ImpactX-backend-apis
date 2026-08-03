namespace ImpactX.Core.Identity;

public static class EmailNormalizer
{
    public static string Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        return email.Trim().ToLowerInvariant();
    }
}
