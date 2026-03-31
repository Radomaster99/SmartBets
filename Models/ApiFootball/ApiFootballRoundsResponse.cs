using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballRoundsResponse
{
    [JsonPropertyName("response")]
    public List<string> Response { get; set; } = new();
}
