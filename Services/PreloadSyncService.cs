using Microsoft.EntityFrameworkCore;
using SmartBets.Data;

namespace SmartBets.Services;

public class PreloadSyncLeagueResult
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }

    public int TeamsProcessed { get; set; }
    public int TeamsInserted { get; set; }
    public int TeamsUpdated { get; set; }

    public int FixturesProcessed { get; set; }
    public int FixturesInserted { get; set; }
    public int FixturesUpdated { get; set; }
    public int FixturesSkippedMissingTeams { get; set; }

    public int StandingsProcessed { get; set; }
    public int StandingsInserted { get; set; }
    public int StandingsUpdated { get; set; }
    public int StandingsSkippedMissingTeams { get; set; }

    public int TeamStatisticsTeamsConsidered { get; set; }
    public int TeamStatisticsTeamsSynced { get; set; }
    public int TeamStatisticsTeamsSkippedFresh { get; set; }

    public List<string> SkippedFeatures { get; set; } = new();
    public string Status { get; set; } = "Success";
    public string? Error { get; set; }
}

public class PreloadRunOptions
{
    public int? Season { get; set; }
    public int? MaxLeagues { get; set; }
    public bool Force { get; set; }
    public bool StopOnRateLimit { get; set; } = true;
    public int MinMinutesSinceLastSync { get; set; } = 180;
}

public class PreloadSyncResult
{
    public bool CountriesSynced { get; set; }
    public bool LeaguesSynced { get; set; }
    public int SupportedLeaguesCount { get; set; }
    public bool StoppedEarly { get; set; }
    public string? StopReason { get; set; }
    public List<PreloadSyncLeagueResult> Leagues { get; set; } = new();
}

public class PreloadSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly CountrySyncService _countrySyncService;
    private readonly LeagueSyncService _leagueSyncService;
    private readonly TeamSyncService _teamSyncService;
    private readonly TeamAnalyticsService _teamAnalyticsService;
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly StandingsSyncService _standingsSyncService;
    private readonly LeagueCoverageService _leagueCoverageService;
    private readonly SyncErrorService _syncErrorService;
    private readonly SyncStateService _syncStateService;

    public PreloadSyncService(
        AppDbContext dbContext,
        CountrySyncService countrySyncService,
        LeagueSyncService leagueSyncService,
        TeamSyncService teamSyncService,
        TeamAnalyticsService teamAnalyticsService,
        FixtureSyncService fixtureSyncService,
        StandingsSyncService standingsSyncService,
        LeagueCoverageService leagueCoverageService,
        SyncErrorService syncErrorService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _countrySyncService = countrySyncService;
        _leagueSyncService = leagueSyncService;
        _teamSyncService = teamSyncService;
        _teamAnalyticsService = teamAnalyticsService;
        _fixtureSyncService = fixtureSyncService;
        _standingsSyncService = standingsSyncService;
        _leagueCoverageService = leagueCoverageService;
        _syncErrorService = syncErrorService;
        _syncStateService = syncStateService;
    }

    public Task<PreloadSyncResult> RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(new PreloadRunOptions(), cancellationToken);
    }

    public async Task<PreloadSyncResult> RunAsync(
        PreloadRunOptions? options,
        CancellationToken cancellationToken = default)
    {
        options ??= new PreloadRunOptions();

        var nowUtc = DateTime.UtcNow;
        var result = new PreloadSyncResult();
        var syncStateUpdates = new List<SyncStateUpsertItem>();
        var minMinutesSinceLastSync = Math.Clamp(options.MinMinutesSinceLastSync, 0, 7 * 24 * 60);

        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var syncStateLookup = syncStates.ToDictionary(
            x => BuildSyncKey(x.EntityType, x.LeagueApiId, x.Season),
            x => x.LastSyncedAt,
            StringComparer.Ordinal);

        bool ShouldSync(string entityType, long? leagueApiId, int? season)
        {
            if (options.Force)
                return true;

            if (minMinutesSinceLastSync <= 0)
                return true;

            var key = BuildSyncKey(entityType, leagueApiId, season);

            if (!syncStateLookup.TryGetValue(key, out var lastSyncedAt))
                return true;

            return (nowUtc - lastSyncedAt).TotalMinutes >= minMinutesSinceLastSync;
        }

        void RegisterSync(string entityType, long? leagueApiId, int? season)
        {
            syncStateLookup[BuildSyncKey(entityType, leagueApiId, season)] = nowUtc;
            syncStateUpdates.Add(new SyncStateUpsertItem
            {
                EntityType = entityType,
                LeagueApiId = leagueApiId,
                Season = season,
                SyncedAtUtc = nowUtc
            });
        }

        // 1. Countries
        if (ShouldSync("countries", null, null))
        {
            await _countrySyncService.SyncCountriesAsync(cancellationToken);
            RegisterSync("countries", null, null);
            result.CountriesSynced = true;
        }

        // 2. Leagues
        if (ShouldSync("leagues", null, null))
        {
            await _leagueSyncService.SyncLeaguesAsync(cancellationToken);
            RegisterSync("leagues", null, null);
            result.LeaguesSynced = true;
        }

        // 3. Supported leagues
        var supportedLeaguesQuery = _dbContext.SupportedLeagues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .AsQueryable();

        if (options.Season.HasValue)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Where(x => x.Season == options.Season.Value);
        }

        if (options.MaxLeagues.HasValue)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Take(Math.Clamp(options.MaxLeagues.Value, 1, 500));
        }

        var supportedLeagues = await supportedLeaguesQuery.ToListAsync(cancellationToken);

        result.SupportedLeaguesCount = supportedLeagues.Count;

        for (var index = 0; index < supportedLeagues.Count; index++)
        {
            var supportedLeague = supportedLeagues[index];
            var leagueResult = new PreloadSyncLeagueResult
            {
                LeagueApiId = supportedLeague.LeagueApiId,
                Season = supportedLeague.Season
            };

            try
            {
                var leagueExists = await _dbContext.Leagues
                    .AnyAsync(
                        x => x.ApiLeagueId == supportedLeague.LeagueApiId &&
                             x.Season == supportedLeague.Season,
                        cancellationToken);

                if (!leagueExists)
                {
                    leagueResult.Status = "Failed";
                    leagueResult.Error =
                        $"League with apiLeagueId {supportedLeague.LeagueApiId} and season {supportedLeague.Season} was not found in database after leagues sync.";

                    result.Leagues.Add(leagueResult);
                    continue;
                }

                // Teams
                if (!ShouldSync("teams", supportedLeague.LeagueApiId, supportedLeague.Season))
                {
                    leagueResult.SkippedFeatures.Add("teams_recently_synced");
                }
                else
                {
                    var teamResult = await _teamSyncService.SyncTeamsAsync(
                        supportedLeague.LeagueApiId,
                        supportedLeague.Season,
                        cancellationToken);

                    leagueResult.TeamsProcessed = teamResult.Processed;
                    leagueResult.TeamsInserted = teamResult.Inserted;
                    leagueResult.TeamsUpdated = teamResult.Updated;

                    RegisterSync("teams", supportedLeague.LeagueApiId, supportedLeague.Season);
                }

                var coverage = await _leagueCoverageService.GetCoverageAsync(
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    cancellationToken);

                if (coverage is not null && !coverage.HasFixtures)
                {
                    leagueResult.SkippedFeatures.Add("fixtures_upcoming");
                }
                else if (!ShouldSync("fixtures_upcoming", supportedLeague.LeagueApiId, supportedLeague.Season))
                {
                    leagueResult.SkippedFeatures.Add("fixtures_upcoming_recently_synced");
                }
                else
                {
                    var fixtureResult = await _fixtureSyncService.SyncUpcomingFixturesAsync(
                        supportedLeague.LeagueApiId,
                        supportedLeague.Season,
                        cancellationToken);

                    leagueResult.FixturesProcessed = fixtureResult.Processed;
                    leagueResult.FixturesInserted = fixtureResult.Inserted;
                    leagueResult.FixturesUpdated = fixtureResult.Updated;
                    leagueResult.FixturesSkippedMissingTeams = fixtureResult.SkippedMissingTeams;

                    RegisterSync("fixtures_upcoming", supportedLeague.LeagueApiId, supportedLeague.Season);
                }

                if (coverage is not null && !coverage.HasStandings)
                {
                    leagueResult.SkippedFeatures.Add("standings");
                }
                else if (!ShouldSync("standings", supportedLeague.LeagueApiId, supportedLeague.Season))
                {
                    leagueResult.SkippedFeatures.Add("standings_recently_synced");
                }
                else
                {
                    var standingsResult = await _standingsSyncService.SyncStandingsAsync(
                        supportedLeague.LeagueApiId,
                        supportedLeague.Season,
                        cancellationToken);

                    leagueResult.StandingsProcessed = standingsResult.Processed;
                    leagueResult.StandingsInserted = standingsResult.Inserted;
                    leagueResult.StandingsUpdated = standingsResult.Updated;
                    leagueResult.StandingsSkippedMissingTeams = standingsResult.SkippedMissingTeams;

                    RegisterSync("standings", supportedLeague.LeagueApiId, supportedLeague.Season);
                }

                if (coverage is not null && !coverage.HasFixtures)
                {
                    leagueResult.SkippedFeatures.Add("team_statistics");
                }
                else if (!ShouldSync("team_statistics", supportedLeague.LeagueApiId, supportedLeague.Season))
                {
                    leagueResult.SkippedFeatures.Add("team_statistics_recently_synced");
                }
                else
                {
                    var teamStatisticsResult = await _teamAnalyticsService.SyncStatisticsAsync(
                        supportedLeague.LeagueApiId,
                        supportedLeague.Season,
                        maxTeams: 25,
                        force: options.Force,
                        cancellationToken: cancellationToken);

                    leagueResult.TeamStatisticsTeamsConsidered = teamStatisticsResult.TeamsConsidered;
                    leagueResult.TeamStatisticsTeamsSynced = teamStatisticsResult.TeamsSynced;
                    leagueResult.TeamStatisticsTeamsSkippedFresh = teamStatisticsResult.TeamsSkippedFresh;

                    if (teamStatisticsResult.TeamsSynced > 0)
                    {
                        RegisterSync("team_statistics", supportedLeague.LeagueApiId, supportedLeague.Season);
                    }
                }

                leagueResult.Status = "Success";
            }
            catch (Exception ex)
            {
                leagueResult.Status = "Failed";
                leagueResult.Error = ex.Message;

                await _syncErrorService.RecordAsync(
                    "preload",
                    "run",
                    "preload",
                    ex.Message,
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    cancellationToken);

                if (options.StopOnRateLimit && IsRateLimitException(ex))
                {
                    result.StoppedEarly = true;
                    result.StopReason = ex.Message;
                    result.Leagues.Add(leagueResult);

                    foreach (var remainingLeague in supportedLeagues.Skip(index + 1))
                    {
                        result.Leagues.Add(new PreloadSyncLeagueResult
                        {
                            LeagueApiId = remainingLeague.LeagueApiId,
                            Season = remainingLeague.Season,
                            Status = "Skipped",
                            Error = "Skipped because preload stopped after a rate limit error."
                        });
                    }

                    if (syncStateUpdates.Count > 0)
                    {
                        await _syncStateService.SetLastSyncedAtBatchAsync(syncStateUpdates, cancellationToken);
                    }

                    return result;
                }
            }

            result.Leagues.Add(leagueResult);
        }

        if (syncStateUpdates.Count > 0)
        {
            await _syncStateService.SetLastSyncedAtBatchAsync(syncStateUpdates, cancellationToken);
        }

        return result;
    }

    private static bool IsRateLimitException(Exception exception)
    {
        return exception.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSyncKey(string entityType, long? leagueApiId, int? season)
    {
        return $"{entityType}:{leagueApiId?.ToString() ?? "global"}:{season?.ToString() ?? "global"}";
    }
}
