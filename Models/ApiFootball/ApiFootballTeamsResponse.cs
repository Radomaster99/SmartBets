using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballTeamsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballTeamItem> Response { get; set; } = new();
}

public class ApiFootballTeamItem
{
    [JsonPropertyName("team")]
    public ApiFootballTeam Team { get; set; } = new();

    [JsonPropertyName("venue")]
    public object? Venue { get; set; } // не ни трябва за момента
}

public class ApiFootballTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }
}