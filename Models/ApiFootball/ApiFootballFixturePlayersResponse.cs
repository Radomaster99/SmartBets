using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballFixturePlayersResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballFixturePlayersTeamItem> Response { get; set; } = new();
}

public class ApiFootballFixturePlayersTeamItem
{
    [JsonPropertyName("team")]
    public ApiFootballFixturePlayersTeam Team { get; set; } = new();

    [JsonPropertyName("players")]
    public List<ApiFootballFixturePlayersPlayerItem> Players { get; set; } = new();
}

public class ApiFootballFixturePlayersTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    [JsonPropertyName("update")]
    public string? Update { get; set; }
}

public class ApiFootballFixturePlayersPlayerItem
{
    [JsonPropertyName("player")]
    public ApiFootballFixturePlayersPlayer Player { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<ApiFootballFixturePlayerStatisticsBlock> Statistics { get; set; } = new();
}

public class ApiFootballFixturePlayersPlayer
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("photo")]
    public string? Photo { get; set; }
}

public class ApiFootballFixturePlayerStatisticsBlock
{
    [JsonPropertyName("games")]
    public ApiFootballFixturePlayerGamesStats Games { get; set; } = new();

    [JsonPropertyName("offsides")]
    public int? Offsides { get; set; }

    [JsonPropertyName("shots")]
    public ApiFootballFixturePlayerShotsStats Shots { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballFixturePlayerGoalsStats Goals { get; set; } = new();

    [JsonPropertyName("passes")]
    public ApiFootballFixturePlayerPassesStats Passes { get; set; } = new();

    [JsonPropertyName("tackles")]
    public ApiFootballFixturePlayerTacklesStats Tackles { get; set; } = new();

    [JsonPropertyName("duels")]
    public ApiFootballFixturePlayerDuelsStats Duels { get; set; } = new();

    [JsonPropertyName("dribbles")]
    public ApiFootballFixturePlayerDribblesStats Dribbles { get; set; } = new();

    [JsonPropertyName("fouls")]
    public ApiFootballFixturePlayerFoulsStats Fouls { get; set; } = new();

    [JsonPropertyName("cards")]
    public ApiFootballFixturePlayerCardsStats Cards { get; set; } = new();

    [JsonPropertyName("penalty")]
    public ApiFootballFixturePlayerPenaltyStats Penalty { get; set; } = new();
}

public class ApiFootballFixturePlayerGamesStats
{
    [JsonPropertyName("minutes")]
    public int? Minutes { get; set; }

    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("rating")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Rating { get; set; }

    [JsonPropertyName("captain")]
    public bool? Captain { get; set; }

    [JsonPropertyName("substitute")]
    public bool? Substitute { get; set; }
}

public class ApiFootballFixturePlayerShotsStats
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("on")]
    public int? On { get; set; }
}

public class ApiFootballFixturePlayerGoalsStats
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("conceded")]
    public int? Conceded { get; set; }

    [JsonPropertyName("assists")]
    public int? Assists { get; set; }

    [JsonPropertyName("saves")]
    public int? Saves { get; set; }
}

public class ApiFootballFixturePlayerPassesStats
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("key")]
    public int? Key { get; set; }

    [JsonPropertyName("accuracy")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Accuracy { get; set; }
}

public class ApiFootballFixturePlayerTacklesStats
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("blocks")]
    public int? Blocks { get; set; }

    [JsonPropertyName("interceptions")]
    public int? Interceptions { get; set; }
}

public class ApiFootballFixturePlayerDuelsStats
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("won")]
    public int? Won { get; set; }
}

public class ApiFootballFixturePlayerDribblesStats
{
    [JsonPropertyName("attempts")]
    public int? Attempts { get; set; }

    [JsonPropertyName("success")]
    public int? Success { get; set; }

    [JsonPropertyName("past")]
    public int? Past { get; set; }
}

public class ApiFootballFixturePlayerFoulsStats
{
    [JsonPropertyName("drawn")]
    public int? Drawn { get; set; }

    [JsonPropertyName("committed")]
    public int? Committed { get; set; }
}

public class ApiFootballFixturePlayerCardsStats
{
    [JsonPropertyName("yellow")]
    public int? Yellow { get; set; }

    [JsonPropertyName("red")]
    public int? Red { get; set; }
}

public class ApiFootballFixturePlayerPenaltyStats
{
    [JsonPropertyName("won")]
    public int? Won { get; set; }

    [JsonPropertyName("commited")]
    public int? Committed { get; set; }

    [JsonPropertyName("scored")]
    public int? Scored { get; set; }

    [JsonPropertyName("missed")]
    public int? Missed { get; set; }

    [JsonPropertyName("saved")]
    public int? Saved { get; set; }
}
