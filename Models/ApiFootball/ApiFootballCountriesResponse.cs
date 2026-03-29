using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballCountriesResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballCountryItem> Response { get; set; } = new();
}

public class ApiFootballCountryItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("flag")]
    public string? Flag { get; set; }
}