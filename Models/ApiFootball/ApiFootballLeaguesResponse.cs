using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballLeaguesResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballLeagueItem> Response { get; set; } = new();
}

public class ApiFootballLeagueItem
{
    [JsonPropertyName("league")]
    public ApiFootballLeague League { get; set; } = new();

    [JsonPropertyName("country")]
    public ApiFootballLeagueCountry Country { get; set; } = new();

    [JsonPropertyName("seasons")]
    public List<ApiFootballLeagueSeason> Seasons { get; set; } = new();
}

public class ApiFootballLeague
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiFootballLeagueCountry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiFootballLeagueSeason
{
    [JsonPropertyName("year")]
    public int Year { get; set; }
}