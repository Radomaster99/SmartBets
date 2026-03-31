using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballTeamStatisticsResponse
{
    [JsonPropertyName("response")]
    public ApiFootballTeamStatisticsItem? Response { get; set; }
}

public class ApiFootballTeamStatisticsItem
{
    [JsonPropertyName("team")]
    public ApiFootballTeamStatisticsTeam Team { get; set; } = new();

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("fixtures")]
    public ApiFootballTeamStatisticsFixtures Fixtures { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballTeamStatisticsGoals Goals { get; set; } = new();

    [JsonPropertyName("biggest")]
    public ApiFootballTeamStatisticsBiggest Biggest { get; set; } = new();

    [JsonPropertyName("clean_sheet")]
    public ApiFootballTeamStatisticsHomeAwayTotal CleanSheet { get; set; } = new();

    [JsonPropertyName("failed_to_score")]
    public ApiFootballTeamStatisticsHomeAwayTotal FailedToScore { get; set; } = new();
}

public class ApiFootballTeamStatisticsTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballTeamStatisticsFixtures
{
    [JsonPropertyName("played")]
    public ApiFootballTeamStatisticsHomeAwayTotal Played { get; set; } = new();

    [JsonPropertyName("wins")]
    public ApiFootballTeamStatisticsHomeAwayTotal Wins { get; set; } = new();

    [JsonPropertyName("draws")]
    public ApiFootballTeamStatisticsHomeAwayTotal Draws { get; set; } = new();

    [JsonPropertyName("loses")]
    public ApiFootballTeamStatisticsHomeAwayTotal Loses { get; set; } = new();
}

public class ApiFootballTeamStatisticsHomeAwayTotal
{
    [JsonPropertyName("home")]
    public int Home { get; set; }

    [JsonPropertyName("away")]
    public int Away { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class ApiFootballTeamStatisticsGoals
{
    [JsonPropertyName("for")]
    public ApiFootballTeamStatisticsGoalsBreakdown For { get; set; } = new();

    [JsonPropertyName("against")]
    public ApiFootballTeamStatisticsGoalsBreakdown Against { get; set; } = new();
}

public class ApiFootballTeamStatisticsGoalsBreakdown
{
    [JsonPropertyName("total")]
    public ApiFootballTeamStatisticsHomeAwayTotal Total { get; set; } = new();

    [JsonPropertyName("average")]
    public ApiFootballTeamStatisticsAverage Average { get; set; } = new();
}

public class ApiFootballTeamStatisticsAverage
{
    [JsonPropertyName("home")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Home { get; set; }

    [JsonPropertyName("away")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Away { get; set; }

    [JsonPropertyName("total")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Total { get; set; }
}

public class ApiFootballTeamStatisticsBiggest
{
    [JsonPropertyName("streak")]
    public ApiFootballTeamStatisticsStreak Streak { get; set; } = new();

    [JsonPropertyName("wins")]
    public ApiFootballTeamStatisticsScorePair Wins { get; set; } = new();

    [JsonPropertyName("loses")]
    public ApiFootballTeamStatisticsScorePair Loses { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballTeamStatisticsBiggestGoals Goals { get; set; } = new();
}

public class ApiFootballTeamStatisticsStreak
{
    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    [JsonPropertyName("draws")]
    public int Draws { get; set; }

    [JsonPropertyName("loses")]
    public int Loses { get; set; }
}

public class ApiFootballTeamStatisticsScorePair
{
    [JsonPropertyName("home")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Home { get; set; }

    [JsonPropertyName("away")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Away { get; set; }
}

public class ApiFootballTeamStatisticsBiggestGoals
{
    [JsonPropertyName("for")]
    public ApiFootballTeamStatisticsGoalsIntPair For { get; set; } = new();

    [JsonPropertyName("against")]
    public ApiFootballTeamStatisticsGoalsIntPair Against { get; set; } = new();
}

public class ApiFootballTeamStatisticsGoalsIntPair
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}
