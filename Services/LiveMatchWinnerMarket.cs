namespace SmartBets.Services;

public static class LiveMatchWinnerMarket
{
    public const string Contains1X2IlikePattern = "%1x2%";

    public static readonly IReadOnlyList<string> Aliases = new[]
    {
        PreMatchOddsService.DefaultMarketName,
        "1X2",
        "Full Time Result",
        "Fulltime Result"
    };

    public static bool IsMatchWinnerBetName(string? betName)
    {
        var normalized = Normalize(betName);
        return NormalizedAliases.Contains(normalized, StringComparer.Ordinal) ||
               Contains1X2(normalized);
    }

    public static string ToCanonicalBetName(string? betName)
    {
        return IsCanonicalAlias(betName)
            ? PreMatchOddsService.DefaultMarketName
            : string.IsNullOrWhiteSpace(betName)
                ? string.Empty
                : betName.Trim();
    }

    public static IReadOnlyList<string> GetUppercaseAliases()
    {
        return UppercaseAliases;
    }

    private static bool IsCanonicalAlias(string? betName)
    {
        return NormalizedAliases.Contains(Normalize(betName), StringComparer.Ordinal);
    }

    private static bool Contains1X2(string normalizedBetName)
    {
        return normalizedBetName.Contains("1X2", StringComparison.Ordinal);
    }

    private static readonly string[] NormalizedAliases = Aliases
        .Select(Normalize)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static readonly string[] UppercaseAliases = Aliases
        .Select(x => x.Trim().ToUpperInvariant())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}
