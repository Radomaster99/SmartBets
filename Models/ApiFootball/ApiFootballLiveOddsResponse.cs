using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballLiveOddsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballLiveOddsFixtureItem> Response { get; set; } = new();
}

public class ApiFootballLiveOddsFixtureItem
{
    [JsonPropertyName("fixture")]
    public ApiFootballOddsFixture Fixture { get; set; } = new();

    [JsonPropertyName("league")]
    public ApiFootballLiveOddsLeague League { get; set; } = new();

    [JsonPropertyName("bookmakers")]
    public List<ApiFootballLiveOddsBookmaker> Bookmakers { get; set; } = new();

    [JsonPropertyName("update")]
    public string? Update { get; set; }

    [JsonPropertyName("stopped")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Stopped { get; set; }

    [JsonPropertyName("blocked")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Blocked { get; set; }

    [JsonPropertyName("finished")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Finished { get; set; }
}

public class ApiFootballLiveOddsLeague
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("season")]
    public int? Season { get; set; }
}

public class ApiFootballLiveOddsBookmaker
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bets")]
    public List<ApiFootballLiveOddsBet> Bets { get; set; } = new();

    [JsonPropertyName("stopped")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Stopped { get; set; }

    [JsonPropertyName("blocked")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Blocked { get; set; }

    [JsonPropertyName("finished")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Finished { get; set; }
}

public class ApiFootballLiveOddsBet
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<ApiFootballLiveOddsValue> Values { get; set; } = new();

    [JsonPropertyName("stopped")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Stopped { get; set; }

    [JsonPropertyName("blocked")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Blocked { get; set; }

    [JsonPropertyName("finished")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Finished { get; set; }
}

public class ApiFootballLiveOddsValue
{
    [JsonPropertyName("value")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("odd")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Odd { get; set; } = string.Empty;

    [JsonPropertyName("main")]
    [JsonConverter(typeof(FlexibleBooleanConverter))]
    public bool? Main { get; set; }

    [JsonPropertyName("handicap")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Handicap { get; set; }
}

public class ApiFootballLiveBetTypesResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballLiveBetTypeItem> Response { get; set; } = new();
}

public class ApiFootballLiveBetTypeItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class FlexibleBooleanConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.TryGetInt64(out var intValue) ? intValue != 0 : null,
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteBooleanValue(value.Value);
    }

    private static bool? ParseString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (bool.TryParse(value, out var parsedBool))
            return parsedBool;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
            return parsedLong != 0;

        return null;
    }
}
