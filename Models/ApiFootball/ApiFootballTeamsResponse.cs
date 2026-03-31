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
    public ApiFootballTeamVenue Venue { get; set; } = new();
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

    [JsonPropertyName("founded")]
    public int? Founded { get; set; }

    [JsonPropertyName("national")]
    public bool? National { get; set; }
}

public class ApiFootballTeamVenue
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("capacity")]
    public int? Capacity { get; set; }

    [JsonPropertyName("surface")]
    public string? Surface { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}
