using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballOddsBookmakersResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballOddsBookmakerReferenceItem> Response { get; set; } = new();
}

public class ApiFootballOddsBookmakerReferenceItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
