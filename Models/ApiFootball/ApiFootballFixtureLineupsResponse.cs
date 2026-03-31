using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballFixtureLineupsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballFixtureLineupItem> Response { get; set; } = new();
}

public class ApiFootballFixtureLineupItem
{
    [JsonPropertyName("team")]
    public ApiFootballFixtureLineupTeam Team { get; set; } = new();

    [JsonPropertyName("coach")]
    public ApiFootballFixtureLineupCoach Coach { get; set; } = new();

    [JsonPropertyName("formation")]
    public string? Formation { get; set; }

    [JsonPropertyName("startXI")]
    public List<ApiFootballFixtureLineupPlayerWrapper> StartXI { get; set; } = new();

    [JsonPropertyName("substitutes")]
    public List<ApiFootballFixtureLineupPlayerWrapper> Substitutes { get; set; } = new();
}

public class ApiFootballFixtureLineupTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballFixtureLineupCoach
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("photo")]
    public string? Photo { get; set; }
}

public class ApiFootballFixtureLineupPlayerWrapper
{
    [JsonPropertyName("player")]
    public ApiFootballFixtureLineupPlayer Player { get; set; } = new();
}

public class ApiFootballFixtureLineupPlayer
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("pos")]
    public string? Position { get; set; }

    [JsonPropertyName("grid")]
    public string? Grid { get; set; }
}
