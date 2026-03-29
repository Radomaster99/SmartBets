using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballFixturesResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballFixtureItem> Response { get; set; } = new();
}

public class ApiFootballFixtureItem
{
    [JsonPropertyName("fixture")]
    public ApiFootballFixture Fixture { get; set; } = new();

    [JsonPropertyName("league")]
    public ApiFootballFixtureLeague League { get; set; } = new();

    [JsonPropertyName("teams")]
    public ApiFootballFixtureTeams Teams { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballFixtureGoals Goals { get; set; } = new();
}

public class ApiFootballFixture
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("status")]
    public ApiFootballFixtureStatus Status { get; set; } = new();
}

public class ApiFootballFixtureStatus
{
    [JsonPropertyName("short")]
    public string? Short { get; set; }

    [JsonPropertyName("long")]
    public string? Long { get; set; }
}

public class ApiFootballFixtureLeague
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiFootballFixtureTeams
{
    [JsonPropertyName("home")]
    public ApiFootballFixtureTeamSide Home { get; set; } = new();

    [JsonPropertyName("away")]
    public ApiFootballFixtureTeamSide Away { get; set; } = new();
}

public class ApiFootballFixtureTeamSide
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiFootballFixtureGoals
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}