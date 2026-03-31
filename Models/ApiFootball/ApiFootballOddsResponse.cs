using System.Globalization;
using System.Text.Json;
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
    // Use a flexible converter: API sometimes returns numbers instead of strings.
    [JsonPropertyName("value")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("odd")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Odd { get; set; } = string.Empty;
}

/// <summary>
/// Converter that accepts JSON string, number, boolean or null and returns a string.
/// Prevents System.Text.Json from throwing when API returns a number where we expect text.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => string.Empty,
            _ => ReadRawJson(ref reader)
        };
    }

    private static string ReadRawJson(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}