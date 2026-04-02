using System.Text.Json.Serialization;

namespace SmartBets.Models.ApiFootball;

public class ApiFootballLeaguesResponse
{
    [JsonPropertyName("response")]
    public List<ApiFootballLeagueItem> Response { get; set; } = new();
}

public class ApiFootballLeagueItem
{
    [JsonPropertyName("league")]
    public ApiFootballLeague League { get; set; } = new();

    [JsonPropertyName("country")]
    public ApiFootballLeagueCountry Country { get; set; } = new();

    [JsonPropertyName("seasons")]
    public List<ApiFootballLeagueSeason> Seasons { get; set; } = new();
}

public class ApiFootballLeague
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiFootballLeagueCountry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiFootballLeagueSeason
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("current")]
    public bool Current { get; set; }

    [JsonPropertyName("coverage")]
    public ApiFootballLeagueSeasonCoverage? Coverage { get; set; }
}

public class ApiFootballLeagueSeasonCoverage
{
    [JsonPropertyName("fixtures")]
    public ApiFootballLeagueSeasonFixturesCoverage Fixtures { get; set; } = new();

    [JsonPropertyName("standings")]
    public bool Standings { get; set; }

    [JsonPropertyName("players")]
    public bool Players { get; set; }

    [JsonPropertyName("top_scorers")]
    public bool TopScorers { get; set; }

    [JsonPropertyName("top_assists")]
    public bool TopAssists { get; set; }

    [JsonPropertyName("top_cards")]
    public bool TopCards { get; set; }

    [JsonPropertyName("injuries")]
    public bool Injuries { get; set; }

    [JsonPropertyName("predictions")]
    public bool Predictions { get; set; }

    [JsonPropertyName("odds")]
    public bool Odds { get; set; }
}

public class ApiFootballLeagueSeasonFixturesCoverage
{
    [JsonPropertyName("events")]
    public bool Events { get; set; }

    [JsonPropertyName("lineups")]
    public bool Lineups { get; set; }

    [JsonPropertyName("statistics_fixtures")]
    public bool StatisticsFixtures { get; set; }

    [JsonPropertyName("statistics_players")]
    public bool StatisticsPlayers { get; set; }
}
