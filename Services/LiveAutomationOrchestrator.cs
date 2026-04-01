using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Enums;

namespace SmartBets.Services;

public class LiveAutomationOrchestrator
{
    private readonly AppDbContext _dbContext;
    private readonly TeamAnalyticsService _teamAnalyticsService;
    private readonly FixtureLiveStatusSyncService _fixtureLiveStatusSyncService;
    private readonly FixtureMatchCenterSyncService _fixtureMatchCenterSyncService;
    private readonly LiveOddsService _liveOddsService;
    private readonly SyncErrorService _syncErrorService;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<LiveAutomationOptions> _optionsMonitor;
    private readonly IOptionsMonitor<ApiFootballClientOptions> _apiFootballClientOptions;
    private readonly ApiFootballQuotaTelemetryService _quotaTelemetryService;
    private readonly ILogger<LiveAutomationOrchestrator> _logger;

    public LiveAutomationOrchestrator(
        AppDbContext dbContext,
        TeamAnalyticsService teamAnalyticsService,
        FixtureLiveStatusSyncService fixtureLiveStatusSyncService,
        FixtureMatchCenterSyncService fixtureMatchCenterSyncService,
        LiveOddsService liveOddsService,
        SyncErrorService syncErrorService,
        IConfiguration configuration,
        IOptionsMonitor<LiveAutomationOptions> optionsMonitor,
        IOptionsMonitor<ApiFootballClientOptions> apiFootballClientOptions,
        ApiFootballQuotaTelemetryService quotaTelemetryService,
        ILogger<LiveAutomationOrchestrator> logger)
    {
        _dbContext = dbContext;
        _teamAnalyticsService = teamAnalyticsService;
        _fixtureLiveStatusSyncService = fixtureLiveStatusSyncService;
        _fixtureMatchCenterSyncService = fixtureMatchCenterSyncService;
        _liveOddsService = liveOddsService;
        _syncErrorService = syncErrorService;
        _configuration = configuration;
        _optionsMonitor = optionsMonitor;
        _apiFootballClientOptions = apiFootballClientOptions;
        _quotaTelemetryService = quotaTelemetryService;
        _logger = logger;
    }

    public async Task<LiveAutomationCycleResult> RunCycleAsync(
        LiveAutomationWorkerState state,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var actions = new List<string>();

        if (!options.Enabled)
        {
            return LiveAutomationCycleResult.Disabled(options.GetIdleInterval());
        }

        if (string.IsNullOrWhiteSpace(_configuration["ApiFootball:BaseUrl"]) ||
            string.IsNullOrWhiteSpace(_configuration["ApiFootball:ApiKey"]))
        {
            return LiveAutomationCycleResult.ConfigurationMissing(options.GetErrorRetryInterval());
        }

        if (options.EnableLiveOddsAutoSync)
        {
            await SyncLiveBetTypesIfDueAsync(state, options, cancellationToken);
        }

        var snapshot = await BuildSnapshotAsync(options, cancellationToken);

        if (options.ActiveSupportedLeaguesOnly && snapshot.ActiveSupportedLeagueCount == 0)
        {
            return LiveAutomationCycleResult.Idle(
                options.GetIdleInterval(),
                snapshot,
                "no_active_supported_leagues");
        }

        if (options.EnableTeamStatisticsAutoSync &&
            IsDue(state.LastTeamStatisticsRunUtc, DateTime.UtcNow, options.GetTeamStatisticsInterval()))
        {
            var syncedLeagueKeys = await SyncSupportedLeagueTeamStatisticsAsync(options, cancellationToken);
            state.LastTeamStatisticsRunUtc = DateTime.UtcNow;

            if (syncedLeagueKeys.Count > 0)
            {
                actions.Add($"team-statistics:{string.Join(',', syncedLeagueKeys)}");
            }
        }

        if (!snapshot.ShouldUseActiveMode)
        {
            if (actions.Count > 0)
            {
                _logger.LogInformation(
                    "Live automation idle cycle completed with background actions. Actions={Actions}",
                    string.Join(" | ", actions));
            }

            return LiveAutomationCycleResult.Idle(
                options.GetIdleInterval(),
                snapshot,
                "no_live_or_near_kickoff_fixtures");
        }

        var nowUtc = DateTime.UtcNow;
        var quotaSnapshot = _quotaTelemetryService.GetSnapshot(_apiFootballClientOptions.CurrentValue);
        var allowPlayers = quotaSnapshot.Mode == ApiFootballQuotaMode.Normal;
        var allowLiveOdds = quotaSnapshot.Mode == ApiFootballQuotaMode.Normal;
        var maxMatchCenterFixtures = quotaSnapshot.Mode switch
        {
            ApiFootballQuotaMode.Critical => Math.Min(2, options.GetMaxMatchCenterFixtures()),
            ApiFootballQuotaMode.Low => Math.Min(4, options.GetMaxMatchCenterFixtures()),
            _ => options.GetMaxMatchCenterFixtures()
        };

        if (IsDue(state.LastLiveStatusRunUtc, nowUtc, options.GetLiveStatusInterval()))
        {
            var liveStatusResult = await _fixtureLiveStatusSyncService.SyncLiveFixturesAsync(
                activeOnly: options.ActiveSupportedLeaguesOnly,
                cancellationToken: cancellationToken);

            state.LastLiveStatusRunUtc = nowUtc;
            snapshot = await BuildSnapshotAsync(options, cancellationToken);
            actions.Add($"live-status:{liveStatusResult.LiveFixturesReceived}");
        }

        if ((snapshot.LiveFixturesCount > 0 || snapshot.PendingPostFinishFixturesCount > 0) &&
            IsDue(state.LastMatchCenterRunUtc, nowUtc, options.GetMatchCenterInterval()))
        {
            var includePlayers = allowPlayers &&
                                 options.IncludePlayersAutomation &&
                                 snapshot.LiveFixturesCount > 0 &&
                                 snapshot.LiveFixturesCount <= options.GetMaxFixturesForPlayers() &&
                                 IsDue(state.LastPlayersRunUtc, nowUtc, options.GetPlayersInterval());

            var matchCenterResult = await _fixtureMatchCenterSyncService.SyncLiveFixturesAsync(
                maxFixtures: maxMatchCenterFixtures,
                includePlayers: includePlayers,
                force: false,
                cancellationToken: cancellationToken);

            state.LastMatchCenterRunUtc = nowUtc;

            if (includePlayers)
            {
                state.LastPlayersRunUtc = nowUtc;
            }

            actions.Add($"match-center:{matchCenterResult.FixturesSynced}");

            if (includePlayers)
            {
                actions.Add("players:on");
            }
        }

        if (options.EnableLiveOddsAutoSync && allowLiveOdds && snapshot.LiveLeagueApiIds.Count > 0)
        {
            var syncedLeagues = await SyncLiveOddsIfDueAsync(snapshot, state, options, cancellationToken);
            if (syncedLeagues.Count > 0)
            {
                actions.Add($"live-odds:{string.Join(',', syncedLeagues)}");
            }
        }
        else if (options.EnableLiveOddsAutoSync && !allowLiveOdds)
        {
            actions.Add("live-odds:quota_guard");
        }

        if (actions.Count > 0)
        {
            _logger.LogInformation(
                "Live automation cycle completed. Live={LiveCount}, Upcoming={UpcomingCount}, PostFinish={PostFinishCount}, Actions={Actions}",
                snapshot.LiveFixturesCount,
                snapshot.UpcomingOrJustStartedFixturesCount,
                snapshot.PendingPostFinishFixturesCount,
                string.Join(" | ", actions));
        }

        return LiveAutomationCycleResult.Active(options.GetActiveInterval(), snapshot, actions);
    }

    private async Task<IReadOnlyList<string>> SyncSupportedLeagueTeamStatisticsAsync(
        LiveAutomationOptions options,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var activeSupportedLeagues = await _dbContext.SupportedLeagues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .ToListAsync(cancellationToken);

        if (activeSupportedLeagues.Count == 0)
            return Array.Empty<string>();

        var teamStatisticsStates = await _dbContext.SyncStates
            .AsNoTracking()
            .Where(x => x.EntityType == "team_statistics")
            .ToListAsync(cancellationToken);

        var lastSyncedLookup = teamStatisticsStates.ToDictionary(
            x => BuildLeagueSeasonKey(x.LeagueApiId ?? 0, x.Season ?? 0),
            x => x.LastSyncedAt,
            StringComparer.Ordinal);

        var dueLeagues = activeSupportedLeagues
            .Where(x =>
            {
                var key = BuildLeagueSeasonKey(x.LeagueApiId, x.Season);
                return !lastSyncedLookup.TryGetValue(key, out var lastSyncedAt) ||
                       nowUtc - lastSyncedAt >= options.GetTeamStatisticsInterval();
            })
            .OrderBy(x =>
            {
                var key = BuildLeagueSeasonKey(x.LeagueApiId, x.Season);
                return lastSyncedLookup.TryGetValue(key, out var lastSyncedAt)
                    ? lastSyncedAt
                    : DateTime.MinValue;
            })
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .Take(options.GetMaxTeamStatisticsLeaguesPerCycle())
            .ToList();

        if (dueLeagues.Count == 0)
            return Array.Empty<string>();

        var syncedLeagueKeys = new List<string>();

        foreach (var supportedLeague in dueLeagues)
        {
            try
            {
                var result = await _teamAnalyticsService.SyncStatisticsAsync(
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    maxTeams: options.GetTeamStatisticsMaxTeamsPerLeague(),
                    force: false,
                    cancellationToken: cancellationToken);

                if (result.TeamsSynced > 0 || result.TeamsSkippedFresh > 0)
                {
                    syncedLeagueKeys.Add($"{supportedLeague.LeagueApiId}:{supportedLeague.Season}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Automatic team statistics sync failed for league {LeagueApiId}, season {Season}.",
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season);

                await _syncErrorService.RecordAsync(
                    "team_statistics",
                    "background_sync",
                    "live_automation",
                    ex.Message,
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    cancellationToken);
            }
        }

        return syncedLeagueKeys;
    }

    private async Task<LiveAutomationSnapshot> BuildSnapshotAsync(
        LiveAutomationOptions options,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var liveStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Live).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var upcomingStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Upcoming).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var finishedStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Finished).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeSupportedKeys = options.ActiveSupportedLeaguesOnly
            ? await _dbContext.SupportedLeagues
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => new LeagueSeasonKey(x.LeagueApiId, x.Season))
                .ToListAsync(cancellationToken)
            : new List<LeagueSeasonKey>();

        var activeSupportedKeySet = activeSupportedKeys
            .Select(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season))
            .ToHashSet(StringComparer.Ordinal);

        var upcomingThreshold = nowUtc.AddMinutes(options.GetUpcomingLookaheadMinutes());
        var recentKickoffThreshold = nowUtc.AddMinutes(-options.GetKickoffGraceMinutes());
        var finishedThreshold = nowUtc.AddHours(-options.GetPostFinishLookbackHours());
        var maxPostFinishRefreshes = options.GetMaxPostFinishRefreshes();

        var candidateFixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x =>
                x.Status != null &&
                (
                    liveStatuses.Contains(x.Status) ||
                    (upcomingStatuses.Contains(x.Status) &&
                     x.KickoffAt >= recentKickoffThreshold &&
                     x.KickoffAt <= upcomingThreshold) ||
                    (finishedStatuses.Contains(x.Status) &&
                     x.PostFinishMatchCenterSyncCount < maxPostFinishRefreshes &&
                     x.KickoffAt >= finishedThreshold)
                ))
            .Select(x => new AutomationFixtureCandidate
            {
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                Status = x.Status!,
                KickoffAt = x.KickoffAt,
                PostFinishMatchCenterSyncCount = x.PostFinishMatchCenterSyncCount
            })
            .ToListAsync(cancellationToken);

        if (options.ActiveSupportedLeaguesOnly)
        {
            candidateFixtures = candidateFixtures
                .Where(x => activeSupportedKeySet.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season)))
                .ToList();
        }

        var liveFixturesCount = candidateFixtures.Count(x => liveStatuses.Contains(x.Status));
        var upcomingOrJustStartedFixturesCount = candidateFixtures.Count(x =>
            upcomingStatuses.Contains(x.Status) &&
            x.KickoffAt >= recentKickoffThreshold &&
            x.KickoffAt <= upcomingThreshold);
        var pendingPostFinishFixturesCount = candidateFixtures.Count(x =>
            finishedStatuses.Contains(x.Status) &&
            x.PostFinishMatchCenterSyncCount < maxPostFinishRefreshes &&
            x.KickoffAt >= finishedThreshold);

        return new LiveAutomationSnapshot
        {
            ActiveSupportedLeagueCount = activeSupportedKeys.Count,
            LiveFixturesCount = liveFixturesCount,
            UpcomingOrJustStartedFixturesCount = upcomingOrJustStartedFixturesCount,
            PendingPostFinishFixturesCount = pendingPostFinishFixturesCount,
            LiveLeagueApiIds = candidateFixtures
                .Where(x => liveStatuses.Contains(x.Status))
                .Select(x => x.LeagueApiId)
                .Distinct()
                .OrderBy(x => x)
                .ToList()
        };
    }

    private async Task SyncLiveBetTypesIfDueAsync(
        LiveAutomationWorkerState state,
        LiveAutomationOptions options,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        if (!IsDue(state.LastLiveBetTypesRunUtc, nowUtc, options.GetLiveBetTypesRefreshInterval()))
        {
            return;
        }

        var result = await _liveOddsService.SyncLiveBetTypesAsync(cancellationToken);
        state.LastLiveBetTypesRunUtc = nowUtc;

        _logger.LogInformation(
            "Live bet types auto-sync completed. Processed={Processed}, Inserted={Inserted}, Updated={Updated}",
            result.Processed,
            result.Inserted,
            result.Updated);
    }

    private async Task<IReadOnlyList<long>> SyncLiveOddsIfDueAsync(
        LiveAutomationSnapshot snapshot,
        LiveAutomationWorkerState state,
        LiveAutomationOptions options,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var dueLeagueIds = snapshot.LiveLeagueApiIds
            .Where(x => !state.LastLiveOddsRunByLeagueApiId.TryGetValue(x, out var lastRun) ||
                        nowUtc - lastRun >= options.GetLiveOddsInterval())
            .OrderBy(x => state.LastLiveOddsRunByLeagueApiId.TryGetValue(x, out var lastRun) ? lastRun : DateTime.MinValue)
            .Take(options.GetMaxLiveOddsLeaguesPerCycle())
            .ToList();

        if (dueLeagueIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        var betIds = options.GetNormalizedLiveOddsBetIds();
        if (betIds.Count == 0 && !options.AllowAllLiveOddsMarkets)
        {
            _logger.LogDebug("Live odds auto-sync skipped because no LiveOddsBetIds are configured and AllowAllLiveOddsMarkets=false.");
            return Array.Empty<long>();
        }

        foreach (var leagueApiId in dueLeagueIds)
        {
            if (betIds.Count == 0)
            {
                await _liveOddsService.SyncLiveOddsAsync(
                    leagueId: leagueApiId,
                    cancellationToken: cancellationToken);
            }
            else
            {
                foreach (var betId in betIds)
                {
                    await _liveOddsService.SyncLiveOddsAsync(
                        leagueId: leagueApiId,
                        betId: betId,
                        cancellationToken: cancellationToken);
                }
            }

            state.LastLiveOddsRunByLeagueApiId[leagueApiId] = nowUtc;
        }

        return dueLeagueIds;
    }

    private static bool IsDue(DateTime? lastRunUtc, DateTime nowUtc, TimeSpan interval)
    {
        return !lastRunUtc.HasValue || nowUtc - lastRunUtc.Value >= interval;
    }

    private static string BuildLeagueSeasonKey(long leagueApiId, int season)
    {
        return $"{leagueApiId}:{season}";
    }

    private sealed class AutomationFixtureCandidate
    {
        public long ApiFixtureId { get; set; }
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime KickoffAt { get; set; }
        public int PostFinishMatchCenterSyncCount { get; set; }
    }

    private sealed record LeagueSeasonKey(long LeagueApiId, int Season);
}

public sealed class LiveAutomationWorkerState
{
    public DateTime? LastLiveStatusRunUtc { get; set; }
    public DateTime? LastMatchCenterRunUtc { get; set; }
    public DateTime? LastPlayersRunUtc { get; set; }
    public DateTime? LastTeamStatisticsRunUtc { get; set; }
    public DateTime? LastLiveBetTypesRunUtc { get; set; }
    public Dictionary<long, DateTime> LastLiveOddsRunByLeagueApiId { get; } = new();
}

public sealed class LiveAutomationCycleResult
{
    public string Mode { get; init; } = "idle";
    public TimeSpan NextDelay { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public LiveAutomationSnapshot Snapshot { get; init; } = new();

    public static LiveAutomationCycleResult Disabled(TimeSpan nextDelay)
    {
        return new LiveAutomationCycleResult
        {
            Mode = "disabled",
            NextDelay = nextDelay
        };
    }

    public static LiveAutomationCycleResult ConfigurationMissing(TimeSpan nextDelay)
    {
        return new LiveAutomationCycleResult
        {
            Mode = "missing-config",
            NextDelay = nextDelay
        };
    }

    public static LiveAutomationCycleResult Idle(
        TimeSpan nextDelay,
        LiveAutomationSnapshot snapshot,
        string reason)
    {
        return new LiveAutomationCycleResult
        {
            Mode = reason,
            NextDelay = nextDelay,
            Snapshot = snapshot
        };
    }

    public static LiveAutomationCycleResult Active(
        TimeSpan nextDelay,
        LiveAutomationSnapshot snapshot,
        IReadOnlyList<string> actions)
    {
        return new LiveAutomationCycleResult
        {
            Mode = "active",
            NextDelay = nextDelay,
            Snapshot = snapshot,
            Actions = actions
        };
    }
}

public sealed class LiveAutomationSnapshot
{
    public int ActiveSupportedLeagueCount { get; init; }
    public int LiveFixturesCount { get; init; }
    public int UpcomingOrJustStartedFixturesCount { get; init; }
    public int PendingPostFinishFixturesCount { get; init; }
    public IReadOnlyList<long> LiveLeagueApiIds { get; init; } = Array.Empty<long>();

    public bool ShouldUseActiveMode =>
        LiveFixturesCount > 0 ||
        UpcomingOrJustStartedFixturesCount > 0 ||
        PendingPostFinishFixturesCount > 0;
}
