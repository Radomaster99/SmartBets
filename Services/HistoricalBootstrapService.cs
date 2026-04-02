using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;

namespace SmartBets.Services;

public class HistoricalBootstrapRunOptions
{
    public int FromSeason { get; set; } = 2023;
    public int? ToSeason { get; set; }
    public int? MaxLeagueSeasons { get; set; }
    public bool Force { get; set; }
    public bool StopOnRateLimit { get; set; } = true;
    public int MinMinutesSinceLastSync { get; set; } = 1440;
    public bool IncludeOdds { get; set; }
    public bool ExcludeAutomationWindow { get; set; } = true;
}

public class HistoricalBootstrapLeagueResult
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public int TeamsProcessed { get; set; }
    public int TeamsInserted { get; set; }
    public int TeamsUpdated { get; set; }
    public int FixturesProcessed { get; set; }
    public int FixturesInserted { get; set; }
    public int FixturesUpdated { get; set; }
    public int FixturesSkippedMissingTeams { get; set; }
    public int OddsFixturesMatched { get; set; }
    public int OddsSnapshotsInserted { get; set; }
    public int OddsSnapshotsProcessed { get; set; }
    public List<string> SkippedFeatures { get; set; } = new();
    public string Status { get; set; } = "Success";
    public string? Error { get; set; }
}

public class HistoricalBootstrapResult
{
    public bool CountriesSynced { get; set; }
    public bool LeaguesSynced { get; set; }
    public bool BookmakersReferenceSynced { get; set; }
    public int LeagueSeasonsSelected { get; set; }
    public bool StoppedEarly { get; set; }
    public string? StopReason { get; set; }
    public List<HistoricalBootstrapLeagueResult> Leagues { get; set; } = new();
}

public class HistoricalBootstrapService
{
    private readonly AppDbContext _dbContext;
    private readonly CountrySyncService _countrySyncService;
    private readonly LeagueSyncService _leagueSyncService;
    private readonly TeamSyncService _teamSyncService;
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly PreMatchOddsService _preMatchOddsService;
    private readonly BookmakerSyncService _bookmakerSyncService;
    private readonly LeagueCoverageService _leagueCoverageService;
    private readonly SyncErrorService _syncErrorService;
    private readonly SyncStateService _syncStateService;
    private readonly IOptionsMonitor<CoreDataAutomationOptions> _automationOptions;

    public HistoricalBootstrapService(
        AppDbContext dbContext,
        CountrySyncService countrySyncService,
        LeagueSyncService leagueSyncService,
        TeamSyncService teamSyncService,
        FixtureSyncService fixtureSyncService,
        PreMatchOddsService preMatchOddsService,
        BookmakerSyncService bookmakerSyncService,
        LeagueCoverageService leagueCoverageService,
        SyncErrorService syncErrorService,
        SyncStateService syncStateService,
        IOptionsMonitor<CoreDataAutomationOptions> automationOptions)
    {
        _dbContext = dbContext;
        _countrySyncService = countrySyncService;
        _leagueSyncService = leagueSyncService;
        _teamSyncService = teamSyncService;
        _fixtureSyncService = fixtureSyncService;
        _preMatchOddsService = preMatchOddsService;
        _bookmakerSyncService = bookmakerSyncService;
        _leagueCoverageService = leagueCoverageService;
        _syncErrorService = syncErrorService;
        _syncStateService = syncStateService;
        _automationOptions = automationOptions;
    }

    public async Task<HistoricalBootstrapResult> RunAsync(
        HistoricalBootstrapRunOptions? options,
        CancellationToken cancellationToken = default)
    {
        options ??= new HistoricalBootstrapRunOptions();

        var nowUtc = DateTime.UtcNow;
        var result = new HistoricalBootstrapResult();
        var syncStateUpdates = new List<SyncStateUpsertItem>();
        var minMinutesSinceLastSync = Math.Clamp(options.MinMinutesSinceLastSync, 0, 30 * 24 * 60);
        var toSeason = options.ToSeason ?? nowUtc.Year;
        var automationMinSeason = nowUtc.Year - _automationOptions.CurrentValue.GetAutomationSeasonLookbackYears();
        var automationMaxSeason = nowUtc.Year + _automationOptions.CurrentValue.GetAutomationSeasonLookaheadYears();

        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var syncStateLookup = syncStates.ToDictionary(
            x => BuildSyncKey(x.EntityType, x.LeagueApiId, x.Season),
            x => x.LastSyncedAt,
            StringComparer.Ordinal);

        bool ShouldSync(string entityType, long? leagueApiId, int? season)
        {
            if (options.Force || minMinutesSinceLastSync <= 0)
                return true;

            if (!syncStateLookup.TryGetValue(BuildSyncKey(entityType, leagueApiId, season), out var lastSyncedAt))
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

        await _countrySyncService.SyncCountriesAsync(cancellationToken);
        RegisterSync("countries", null, null);
        result.CountriesSynced = true;

        await _leagueSyncService.SyncLeaguesAsync(cancellationToken);
        RegisterSync("leagues", null, null);
        result.LeaguesSynced = true;

        await _bookmakerSyncService.SyncReferenceBookmakersAsync(cancellationToken);
        RegisterSync("bookmakers_reference", null, null);
        result.BookmakersReferenceSynced = true;

        var historicalLeaguesQuery = _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => x.Season >= options.FromSeason && x.Season <= toSeason);

        if (options.ExcludeAutomationWindow)
        {
            historicalLeaguesQuery = historicalLeaguesQuery
                .Where(x => x.Season < automationMinSeason || x.Season > automationMaxSeason);
        }

        historicalLeaguesQuery = historicalLeaguesQuery
            .OrderByDescending(x => x.Season)
            .ThenBy(x => x.Country.Name)
            .ThenBy(x => x.Name);

        if (options.MaxLeagueSeasons.HasValue)
        {
            historicalLeaguesQuery = historicalLeaguesQuery.Take(Math.Clamp(options.MaxLeagueSeasons.Value, 1, 5000));
        }

        var historicalLeagues = await historicalLeaguesQuery.ToListAsync(cancellationToken);
        result.LeagueSeasonsSelected = historicalLeagues.Count;

        foreach (var league in historicalLeagues)
        {
            var leagueResult = new HistoricalBootstrapLeagueResult
            {
                LeagueApiId = league.ApiLeagueId,
                Season = league.Season,
                LeagueName = league.Name,
                CountryName = league.Country.Name
            };

            try
            {
                if (!ShouldSync("teams", league.ApiLeagueId, league.Season))
                {
                    leagueResult.SkippedFeatures.Add("teams_recently_synced");
                }
                else
                {
                    var teamResult = await _teamSyncService.SyncTeamsAsync(league.ApiLeagueId, league.Season, cancellationToken);
                    leagueResult.TeamsProcessed = teamResult.Processed;
                    leagueResult.TeamsInserted = teamResult.Inserted;
                    leagueResult.TeamsUpdated = teamResult.Updated;
                    RegisterSync("teams", league.ApiLeagueId, league.Season);
                }

                var coverage = await _leagueCoverageService.GetCoverageAsync(league.ApiLeagueId, league.Season, cancellationToken);

                if (coverage is not null && !coverage.HasFixtures)
                {
                    leagueResult.SkippedFeatures.Add("fixtures_unsupported");
                }
                else if (!ShouldSync("fixtures_full", league.ApiLeagueId, league.Season))
                {
                    leagueResult.SkippedFeatures.Add("fixtures_full_recently_synced");
                }
                else
                {
                    var fixtureResult = await _fixtureSyncService.SyncFixturesAsync(league.ApiLeagueId, league.Season, cancellationToken);
                    leagueResult.FixturesProcessed = fixtureResult.Processed;
                    leagueResult.FixturesInserted = fixtureResult.Inserted;
                    leagueResult.FixturesUpdated = fixtureResult.Updated;
                    leagueResult.FixturesSkippedMissingTeams = fixtureResult.SkippedMissingTeams;
                    RegisterSync("fixtures_full", league.ApiLeagueId, league.Season);
                    RegisterSync("fixtures_upcoming", league.ApiLeagueId, league.Season);
                }

                if (!options.IncludeOdds)
                {
                    leagueResult.SkippedFeatures.Add("odds_not_requested");
                }
                else if (coverage is not null && !coverage.HasOdds)
                {
                    leagueResult.SkippedFeatures.Add("odds_unsupported");
                }
                else if (!ShouldSync("odds", league.ApiLeagueId, league.Season))
                {
                    leagueResult.SkippedFeatures.Add("odds_recently_synced");
                }
                else
                {
                    var oddsResult = await _preMatchOddsService.SyncOddsAsync(
                        league.ApiLeagueId,
                        league.Season,
                        cancellationToken: cancellationToken);

                    leagueResult.OddsFixturesMatched = oddsResult.FixturesMatched;
                    leagueResult.OddsSnapshotsInserted = oddsResult.SnapshotsInserted;
                    leagueResult.OddsSnapshotsProcessed = oddsResult.SnapshotsProcessed;
                    RegisterSync("odds", league.ApiLeagueId, league.Season);
                    RegisterSync("bookmakers", league.ApiLeagueId, league.Season);
                }
            }
            catch (Exception ex)
            {
                leagueResult.Status = "Failed";
                leagueResult.Error = ex.Message;

                await _syncErrorService.RecordAsync(
                    "historical_bootstrap",
                    "run",
                    "historical_bootstrap",
                    ex.Message,
                    league.ApiLeagueId,
                    league.Season,
                    cancellationToken);

                if (options.StopOnRateLimit && IsRateLimitException(ex))
                {
                    result.StoppedEarly = true;
                    result.StopReason = ex.Message;
                    result.Leagues.Add(leagueResult);
                    break;
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
