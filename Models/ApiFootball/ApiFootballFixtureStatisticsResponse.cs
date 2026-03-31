using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballFixtureStatisticsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballFixtureStatisticsItem> Response { get; set; } = new();
}

public class ApiFootballFixtureStatisticsItem
{
    [JsonPropertyName("team")]
    public ApiFootballFixtureStatisticsTeam Team { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<ApiFootballFixtureStatisticValueItem> Statistics { get; set; } = new();
}

public class ApiFootballFixtureStatisticsTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballFixtureStatisticValueItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Value { get; set; }
}
