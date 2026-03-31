using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballFixtureEventsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballFixtureEventItem> Response { get; set; } = new();
}

public class ApiFootballFixtureEventItem
{
    [JsonPropertyName("time")]
    public ApiFootballFixtureEventTime Time { get; set; } = new();

    [JsonPropertyName("team")]
    public ApiFootballFixtureEventTeam Team { get; set; } = new();

    [JsonPropertyName("player")]
    public ApiFootballFixtureEventPerson Player { get; set; } = new();

    [JsonPropertyName("assist")]
    public ApiFootballFixtureEventPerson Assist { get; set; } = new();

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("comments")]
    public string? Comments { get; set; }
}

public class ApiFootballFixtureEventTime
{
    [JsonPropertyName("elapsed")]
    public int? Elapsed { get; set; }

    [JsonPropertyName("extra")]
    public int? Extra { get; set; }
}

public class ApiFootballFixtureEventTeam
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballFixtureEventPerson
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
