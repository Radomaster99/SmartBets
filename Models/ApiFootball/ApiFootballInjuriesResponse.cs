using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballInjuriesResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballInjuryItem> Response { get; set; } = new();
}

public class ApiFootballInjuryItem
{
    [JsonPropertyName("team")]
    public ApiFootballInjuryTeam Team { get; set; } = new();

    [JsonPropertyName("player")]
    public ApiFootballInjuryPlayer Player { get; set; } = new();
}

public class ApiFootballInjuryTeam
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballInjuryPlayer
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("photo")]
    public string? Photo { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
