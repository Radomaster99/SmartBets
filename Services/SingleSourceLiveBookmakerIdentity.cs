namespace SmartBets.Services;

public static class SingleSourceLiveBookmakerIdentity
{
    public const long SyntheticApiBookmakerId = 0;
    public const string Name = "Bet365";

    public static bool IsSingleSourceName(string? bookmakerName)
    {
        return string.Equals(
            NormalizeName(bookmakerName),
            NormalizeName(Name),
            StringComparison.Ordinal);
    }

    public static string NormalizeName(string? bookmakerName)
    {
        if (string.IsNullOrWhiteSpace(bookmakerName))
            return string.Empty;

        return new string(bookmakerName
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
