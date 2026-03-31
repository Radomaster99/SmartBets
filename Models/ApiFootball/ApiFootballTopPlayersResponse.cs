using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballTopPlayersResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballTopPlayerItem> Response { get; set; } = new();
}

public class ApiFootballTopPlayerItem
{
    [JsonPropertyName("player")]
    public ApiFootballTopPlayerInfo Player { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<ApiFootballTopPlayerStatisticsBlock> Statistics { get; set; } = new();
}

public class ApiFootballTopPlayerInfo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("photo")]
    public string? Photo { get; set; }
}

public class ApiFootballTopPlayerStatisticsBlock
{
    [JsonPropertyName("team")]
    public ApiFootballTopPlayerTeam Team { get; set; } = new();

    [JsonPropertyName("games")]
    public ApiFootballTopPlayerGames Games { get; set; } = new();

    [JsonPropertyName("shots")]
    public ApiFootballTopPlayerShots Shots { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballTopPlayerGoals Goals { get; set; } = new();

    [JsonPropertyName("passes")]
    public ApiFootballTopPlayerPasses Passes { get; set; } = new();

    [JsonPropertyName("cards")]
    public ApiFootballTopPlayerCards Cards { get; set; } = new();

    [JsonPropertyName("penalty")]
    public ApiFootballTopPlayerPenalty Penalty { get; set; } = new();
}

public class ApiFootballTopPlayerTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiFootballTopPlayerGames
{
    [JsonPropertyName("appearences")]
    public int? Appearances { get; set; }

    [JsonPropertyName("minutes")]
    public int? Minutes { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("rating")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Rating { get; set; }
}

public class ApiFootballTopPlayerShots
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("on")]
    public int? On { get; set; }
}

public class ApiFootballTopPlayerGoals
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("assists")]
    public int? Assists { get; set; }
}

public class ApiFootballTopPlayerPasses
{
    [JsonPropertyName("key")]
    public int? Key { get; set; }
}

public class ApiFootballTopPlayerCards
{
    [JsonPropertyName("yellow")]
    public int? Yellow { get; set; }

    [JsonPropertyName("red")]
    public int? Red { get; set; }
}

public class ApiFootballTopPlayerPenalty
{
    [JsonPropertyName("scored")]
    public int? Scored { get; set; }
}
