using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Enums;

namespace SmartBets.Services;

public class LiveAutomationOrchestrator
{
    private readonly AppDbContext _dbContext;
    private readonly FixtureLiveStatusSyncService _fixtureLiveStatusSyncService;
    private readonly FixtureMatchCenterSyncService _fixtureMatchCenterSyncService;
    private readonly LiveOddsService _liveOddsService;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<LiveAutomationOptions> _optionsMonitor;
    private readonly ILogger<LiveAutomationOrchestrator> _logger;

    public LiveAutomationOrchestrator(
        AppDbContext dbContext,
        FixtureLiveStatusSyncService fixtureLiveStatusSyncService,
        FixtureMatchCenterSyncService fixtureMatchCenterSyncService,
        LiveOddsService liveOddsService,
        IConfiguration configuration,
        IOptionsMonitor<LiveAutomationOptions> optionsMonitor,
        ILogger<LiveAutomationOrchestrator> logger)
    {
        _dbContext = dbContext;
        _fixtureLiveStatusSyncService = fixtureLiveStatusSyncService;
        _fixtureMatchCenterSyncService = fixtureMatchCenterSyncService;
        _liveOddsService = liveOddsService;
        _configuration = configuration;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<LiveAutomationCycleResult> RunCycleAsync(
        LiveAutomationWorkerState state,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

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

        if (!snapshot.ShouldUseActiveMode)
        {
            return LiveAutomationCycleResult.Idle(
                options.GetIdleInterval(),
                snapshot,
                "no_live_or_near_kickoff_fixtures");
        }

        var actions = new List<string>();
        var nowUtc = DateTime.UtcNow;

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
            var includePlayers = options.IncludePlayersAutomation &&
                                 snapshot.LiveFixturesCount > 0 &&
                                 snapshot.LiveFixturesCount <= options.GetMaxFixturesForPlayers() &&
                                 IsDue(state.LastPlayersRunUtc, nowUtc, options.GetPlayersInterval());

            var matchCenterResult = await _fixtureMatchCenterSyncService.SyncLiveFixturesAsync(
                maxFixtures: options.GetMaxMatchCenterFixtures(),
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

        if (options.EnableLiveOddsAutoSync && snapshot.LiveLeagueApiIds.Count > 0)
        {
            var syncedLeagues = await SyncLiveOddsIfDueAsync(snapshot, state, options, cancellationToken);
            if (syncedLeagues.Count > 0)
            {
                actions.Add($"live-odds:{string.Join(',', syncedLeagues)}");
            }
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
