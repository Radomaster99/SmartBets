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

    [JsonPropertyName("events")]
    public List<ApiFootballFixtureEventItem> Events { get; set; } = new();

    [JsonPropertyName("lineups")]
    public List<ApiFootballFixtureLineupItem> Lineups { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<ApiFootballFixtureStatisticsItem> Statistics { get; set; } = new();

    [JsonPropertyName("players")]
    public List<ApiFootballFixturePlayersTeamItem> Players { get; set; } = new();
}

public class ApiFootballFixture
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("referee")]
    public string? Referee { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("status")]
    public ApiFootballFixtureStatus Status { get; set; } = new();

    [JsonPropertyName("venue")]
    public ApiFootballFixtureVenue Venue { get; set; } = new();
}

public class ApiFootballFixtureStatus
{
    [JsonPropertyName("short")]
    public string? Short { get; set; }

    [JsonPropertyName("long")]
    public string? Long { get; set; }

    [JsonPropertyName("elapsed")]
    public int? Elapsed { get; set; }

    [JsonPropertyName("extra")]
    public int? Extra { get; set; }
}

public class ApiFootballFixtureLeague
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("round")]
    public string? Round { get; set; }
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

public class ApiFootballFixtureVenue
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }
}
