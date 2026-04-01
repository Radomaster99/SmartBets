namespace SmartBets.Services;

public class LiveAutomationOptions
{
    public bool Enabled { get; set; } = true;
    public bool ActiveSupportedLeaguesOnly { get; set; } = true;
    public int ActiveIntervalSeconds { get; set; } = 30;
    public int IdleIntervalSeconds { get; set; } = 300;
    public int ErrorRetrySeconds { get; set; } = 120;
    public int LiveStatusIntervalSeconds { get; set; } = 30;
    public int MatchCenterIntervalSeconds { get; set; } = 60;
    public bool IncludePlayersAutomation { get; set; } = true;
    public int PlayersIntervalSeconds { get; set; } = 180;
    public int MaxFixturesForPlayers { get; set; } = 2;
    public int UpcomingLookaheadMinutes { get; set; } = 75;
    public int KickoffGraceMinutes { get; set; } = 20;
    public int PostFinishLookbackHours { get; set; } = 3;
    public int MaxPostFinishRefreshes { get; set; } = 2;
    public int MaxMatchCenterFixtures { get; set; } = 6;
    public bool EnableTeamStatisticsAutoSync { get; set; } = true;
    public int TeamStatisticsIntervalHours { get; set; } = 24;
    public int MaxTeamStatisticsLeaguesPerCycle { get; set; } = 6;
    public int TeamStatisticsMaxTeamsPerLeague { get; set; } = 25;
    public bool EnableLiveOddsAutoSync { get; set; } = false;
    public bool AllowAllLiveOddsMarkets { get; set; } = false;
    public int LiveOddsIntervalSeconds { get; set; } = 120;
    public int MaxLiveOddsLeaguesPerCycle { get; set; } = 2;
    public int LiveBetTypesRefreshHours { get; set; } = 24;
    public List<long> LiveOddsBetIds { get; set; } = new();

    public TimeSpan GetActiveInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(ActiveIntervalSeconds, 15, 600));
    }

    public TimeSpan GetIdleInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(IdleIntervalSeconds, 60, 3600));
    }

    public TimeSpan GetErrorRetryInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(ErrorRetrySeconds, 30, 3600));
    }

    public TimeSpan GetLiveStatusInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(LiveStatusIntervalSeconds, 15, 300));
    }

    public TimeSpan GetMatchCenterInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(MatchCenterIntervalSeconds, 30, 600));
    }

    public TimeSpan GetPlayersInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(PlayersIntervalSeconds, 60, 1800));
    }

    public TimeSpan GetLiveOddsInterval()
    {
        return TimeSpan.FromSeconds(Math.Clamp(LiveOddsIntervalSeconds, 60, 1800));
    }

    public TimeSpan GetLiveBetTypesRefreshInterval()
    {
        return TimeSpan.FromHours(Math.Clamp(LiveBetTypesRefreshHours, 1, 168));
    }

    public int GetUpcomingLookaheadMinutes()
    {
        return Math.Clamp(UpcomingLookaheadMinutes, 15, 240);
    }

    public int GetKickoffGraceMinutes()
    {
        return Math.Clamp(KickoffGraceMinutes, 0, 90);
    }

    public int GetPostFinishLookbackHours()
    {
        return Math.Clamp(PostFinishLookbackHours, 1, 6);
    }

    public int GetMaxPostFinishRefreshes()
    {
        return Math.Clamp(MaxPostFinishRefreshes, 1, 5);
    }

    public int GetMaxMatchCenterFixtures()
    {
        return Math.Clamp(MaxMatchCenterFixtures, 1, 20);
    }

    public TimeSpan GetTeamStatisticsInterval()
    {
        return TimeSpan.FromHours(Math.Clamp(TeamStatisticsIntervalHours, 6, 72));
    }

    public int GetMaxTeamStatisticsLeaguesPerCycle()
    {
        return Math.Clamp(MaxTeamStatisticsLeaguesPerCycle, 1, 20);
    }

    public int GetTeamStatisticsMaxTeamsPerLeague()
    {
        return Math.Clamp(TeamStatisticsMaxTeamsPerLeague, 1, 40);
    }

    public int GetMaxFixturesForPlayers()
    {
        return Math.Clamp(MaxFixturesForPlayers, 1, 10);
    }

    public int GetMaxLiveOddsLeaguesPerCycle()
    {
        return Math.Clamp(MaxLiveOddsLeaguesPerCycle, 1, 10);
    }

    public IReadOnlyList<long> GetNormalizedLiveOddsBetIds()
    {
        return LiveOddsBetIds
            .Where(x => x > 0)
            .Distinct()
            .Take(5)
            .ToList();
    }
}
