using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballOddsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballOddsFixtureItem> Response { get; set; } = new();
}

public class ApiFootballOddsFixtureItem
{
    [JsonPropertyName("fixture")]
    public ApiFootballOddsFixture Fixture { get; set; } = new();

    [JsonPropertyName("bookmakers")]
    public List<ApiFootballOddsBookmaker> Bookmakers { get; set; } = new();
}

public class ApiFootballOddsFixture
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public class ApiFootballOddsBookmaker
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bets")]
    public List<ApiFootballOddsBet> Bets { get; set; } = new();
}

public class ApiFootballOddsBet
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<ApiFootballOddsValue> Values { get; set; } = new();
}

public class ApiFootballOddsValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("odd")]
    public string Odd { get; set; } = string.Empty;
}