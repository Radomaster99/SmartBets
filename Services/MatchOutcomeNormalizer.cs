using System.Globalization;
using System.Text;

namespace SmartBets.Services;

public static class MatchOutcomeNormalizer
{
    public static bool IsHomeOutcome(string? outcomeLabel, string homeTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = Normalize(outcomeLabel);
        var normalizedHomeTeam = Normalize(homeTeamName);

        return normalized is "1" or "HOME" ||
               normalized == normalizedHomeTeam;
    }

    public static bool IsDrawOutcome(string? outcomeLabel)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = Normalize(outcomeLabel);
        return normalized is "X" or "DRAW" or "TIE";
    }

    public static bool IsAwayOutcome(string? outcomeLabel, string awayTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return false;

        var normalized = Normalize(outcomeLabel);
        var normalizedAwayTeam = Normalize(awayTeamName);

        return normalized is "2" or "AWAY" ||
               normalized == normalizedAwayTeam;
    }

    public static string Canonicalize(string? outcomeLabel, string homeTeamName, string awayTeamName)
    {
        if (string.IsNullOrWhiteSpace(outcomeLabel))
            return string.Empty;

        if (IsHomeOutcome(outcomeLabel, homeTeamName))
            return homeTeamName.Trim();

        if (IsDrawOutcome(outcomeLabel))
            return "Draw";

        if (IsAwayOutcome(outcomeLabel, awayTeamName))
            return awayTeamName.Trim();

        return outcomeLabel.Trim();
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }
}
