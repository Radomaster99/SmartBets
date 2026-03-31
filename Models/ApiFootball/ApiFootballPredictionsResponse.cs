using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballPredictionsResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballPredictionItem> Response { get; set; } = new();
}

public class ApiFootballPredictionItem
{
    [JsonPropertyName("predictions")]
    public ApiFootballPredictionValues Predictions { get; set; } = new();

    [JsonPropertyName("comparison")]
    public ApiFootballPredictionComparison Comparison { get; set; } = new();
}

public class ApiFootballPredictionValues
{
    [JsonPropertyName("winner")]
    public ApiFootballPredictionWinner Winner { get; set; } = new();

    [JsonPropertyName("win_or_draw")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? WinOrDraw { get; set; }

    [JsonPropertyName("under_over")]
    public string? UnderOver { get; set; }

    [JsonPropertyName("goals")]
    public ApiFootballPredictionGoals Goals { get; set; } = new();

    [JsonPropertyName("advice")]
    public string? Advice { get; set; }

    [JsonPropertyName("percent")]
    public ApiFootballPredictionPercent Percent { get; set; } = new();
}

public class ApiFootballPredictionWinner
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

public class ApiFootballPredictionGoals
{
    [JsonPropertyName("home")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Home { get; set; }

    [JsonPropertyName("away")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Away { get; set; }
}

public class ApiFootballPredictionPercent
{
    [JsonPropertyName("home")]
    public string? Home { get; set; }

    [JsonPropertyName("draw")]
    public string? Draw { get; set; }

    [JsonPropertyName("away")]
    public string? Away { get; set; }
}

public class ApiFootballPredictionComparison
{
    [JsonPropertyName("form")]
    public ApiFootballPredictionComparisonPair Form { get; set; } = new();

    [JsonPropertyName("att")]
    public ApiFootballPredictionComparisonPair Attack { get; set; } = new();

    [JsonPropertyName("def")]
    public ApiFootballPredictionComparisonPair Defence { get; set; } = new();

    [JsonPropertyName("poisson_distribution")]
    public ApiFootballPredictionComparisonPair PoissonDistribution { get; set; } = new();

    [JsonPropertyName("h2h")]
    public ApiFootballPredictionComparisonPair HeadToHead { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballPredictionComparisonPair Goals { get; set; } = new();

    [JsonPropertyName("total")]
    public ApiFootballPredictionComparisonPair Total { get; set; } = new();
}

public class ApiFootballPredictionComparisonPair
{
    [JsonPropertyName("home")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Home { get; set; }

    [JsonPropertyName("away")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Away { get; set; }
}
