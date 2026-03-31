using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballStandingsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballStandingsLeagueWrapper> Response { get; set; } = new();
}

public class ApiFootballStandingsLeagueWrapper
{
    [JsonPropertyName("league")]
    public ApiFootballStandingsLeague League { get; set; } = new();
}

public class ApiFootballStandingsLeague
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("standings")]
    public List<List<ApiFootballStandingItem>> Standings { get; set; } = new();
}

public class ApiFootballStandingItem
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("team")]
    public ApiFootballStandingTeam Team { get; set; } = new();

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("goalsDiff")]
    public int GoalsDiff { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("all")]
    public ApiFootballStandingStats Stats { get; set; } = new();
}

public class ApiFootballStandingTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballStandingStats
{
    [JsonPropertyName("played")]
    public int Played { get; set; }

    [JsonPropertyName("win")]
    public int Win { get; set; }

    [JsonPropertyName("draw")]
    public int Draw { get; set; }

    [JsonPropertyName("lose")]
    public int Lose { get; set; }

    [JsonPropertyName("goals")]
    public ApiFootballStandingGoals Goals { get; set; } = new();
}

public class ApiFootballStandingGoals
{
    [JsonPropertyName("for")]
    public int GoalsFor { get; set; }

    [JsonPropertyName("against")]
    public int GoalsAgainst { get; set; }
}