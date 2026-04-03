using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Enums;

namespace SmartBets.Services;

public class CoreDataAutomationOrchestrator
{
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;
    private readonly CoreLeagueCatalogState _coreLeagueCatalogState;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<CoreDataAutomationOptions> _optionsMonitor;
    private readonly IOptionsMonitor<ApiFootballClientOptions> _apiFootballClientOptions;
    private readonly ApiFootballQuotaTelemetryService _quotaTelemetryService;
    private readonly CoreAutomationQuotaManager _quotaManager;
    private readonly CoreAutomationCatalogRefreshJobService _catalogRefreshJobService;
    private readonly CoreAutomationTeamsRollingJobService _teamsRollingJobService;
    private readonly CoreAutomationStandingsRollingJobService _standingsRollingJobService;
    private readonly CoreAutomationFixturesRollingJobService _fixturesRollingJobService;
    private readonly CoreAutomationOddsPreMatchJobService _oddsPreMatchJobService;
    private readonly CoreAutomationOddsLiveJobService _oddsLiveJobService;
    private readonly CoreAutomationRepairJobService _repairJobService;
    private readonly ILogger<CoreDataAutomationOrchestrator> _logger;

    public CoreDataAutomationOrchestrator(
        AppDbContext dbContext,
        SyncStateService syncStateService,
        CoreLeagueCatalogState coreLeagueCatalogState,
        IConfiguration configuration,
        IOptionsMonitor<CoreDataAutomationOptions> optionsMonitor,
        IOptionsMonitor<ApiFootballClientOptions> apiFootballClientOptions,
        ApiFootballQuotaTelemetryService quotaTelemetryService,
        CoreAutomationQuotaManager quotaManager,
        CoreAutomationCatalogRefreshJobService catalogRefreshJobService,
        CoreAutomationTeamsRollingJobService teamsRollingJobService,
        CoreAutomationStandingsRollingJobService standingsRollingJobService,
        CoreAutomationFixturesRollingJobService fixturesRollingJobService,
        CoreAutomationOddsPreMatchJobService oddsPreMatchJobService,
        CoreAutomationOddsLiveJobService oddsLiveJobService,
        CoreAutomationRepairJobService repairJobService,
        ILogger<CoreDataAutomationOrchestrator> logger)
    {
        _dbContext = dbContext;
        _syncStateService = syncStateService;
        _coreLeagueCatalogState = coreLeagueCatalogState;
        _configuration = configuration;
        _optionsMonitor = optionsMonitor;
        _apiFootballClientOptions = apiFootballClientOptions;
        _quotaTelemetryService = quotaTelemetryService;
        _quotaManager = quotaManager;
        _catalogRefreshJobService = catalogRefreshJobService;
        _teamsRollingJobService = teamsRollingJobService;
        _standingsRollingJobService = standingsRollingJobService;
        _fixturesRollingJobService = fixturesRollingJobService;
        _oddsPreMatchJobService = oddsPreMatchJobService;
        _oddsLiveJobService = oddsLiveJobService;
        _repairJobService = repairJobService;
        _logger = logger;
    }

    public async Task<CoreDataAutomationCycleResult> RunCycleAsync(
        CoreDataAutomationWorkerState state,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var actions = new List<string>();

        if (!options.Enabled)
        {
            return CoreDataAutomationCycleResult.Disabled(options.GetIdleInterval());
        }

        if (string.IsNullOrWhiteSpace(_configuration["ApiFootball:BaseUrl"]) ||
            string.IsNullOrWhiteSpace(_configuration["ApiFootball:ApiKey"]))
        {
            return CoreDataAutomationCycleResult.ConfigurationMissing(options.GetErrorRetryInterval());
        }

        var nowUtc = DateTime.UtcNow;
        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var syncLookup = syncStates.ToDictionary(
            x => BuildSyncKey(x.EntityType, x.LeagueApiId, x.Season),
            x => x.LastSyncedAt,
            StringComparer.Ordinal);

        var syncStateUpdates = new List<SyncStateUpsertItem>();

        bool ShouldSync(string entityType, long? leagueApiId, int? season, TimeSpan interval)
        {
            if (!syncLookup.TryGetValue(BuildSyncKey(entityType, leagueApiId, season), out var lastSyncedAt))
                return true;

            return nowUtc - lastSyncedAt >= interval;
        }

        void RegisterSync(string entityType, long? leagueApiId, int? season)
        {
            syncLookup[BuildSyncKey(entityType, leagueApiId, season)] = nowUtc;
            syncStateUpdates.Add(new SyncStateUpsertItem
            {
                EntityType = entityType,
                LeagueApiId = leagueApiId,
                Season = season,
                SyncedAtUtc = nowUtc
            });
        }

        await RunCatalogRefreshJobAsync(
            state,
            nowUtc,
            options,
            ShouldSync,
            RegisterSync,
            actions,
            cancellationToken);

        var currentTargets = _coreLeagueCatalogState.GetTargets();
        if (currentTargets.Count == 0)
        {
            if (syncStateUpdates.Count > 0)
            {
                await _syncStateService.SetLastSyncedAtBatchAsync(syncStateUpdates, cancellationToken);
            }

            return CoreDataAutomationCycleResult.Idle(
                options.GetIdleInterval(),
                new CoreDataAutomationSnapshot(),
                "no_current_league_targets");
        }

        var snapshot = await BuildSnapshotAsync(currentTargets, options, cancellationToken);

        await RunTeamsRollingJobAsync(
            currentTargets,
            syncLookup,
            options,
            ShouldSync,
            RegisterSync,
            actions,
            cancellationToken);

        await RunStandingsRollingJobAsync(
            currentTargets,
            snapshot,
            syncLookup,
            options,
            ShouldSync,
            RegisterSync,
            actions,
            cancellationToken);

        var hotFixtureKeys = await RunFixturesRollingJobAsync(
            state,
            snapshot,
            syncLookup,
            nowUtc,
            options,
            ShouldSync,
            RegisterSync,
            actions,
            cancellationToken);

        if (hotFixtureKeys.Count > 0 || IsRecentlyUpdated(state.LastLiveStatusRunUtc, nowUtc, options.GetLiveStatusInterval()))
        {
            snapshot = await BuildSnapshotAsync(currentTargets, options, cancellationToken);
        }

        await RunOddsPreMatchJobAsync(
            snapshot,
            options,
            RegisterSync,
            actions,
            cancellationToken);

        await RunOddsLiveJobAsync(
            state,
            snapshot,
            syncLookup,
            nowUtc,
            options,
            RegisterSync,
            actions,
            cancellationToken);

        await RunRepairJobAsync(
            state,
            snapshot,
            hotFixtureKeys,
            syncLookup,
            nowUtc,
            options,
            ShouldSync,
            RegisterSync,
            actions,
            cancellationToken);

        if (syncStateUpdates.Count > 0)
        {
            await _syncStateService.SetLastSyncedAtBatchAsync(syncStateUpdates, cancellationToken);
        }

        if (actions.Count > 0)
        {
            _logger.LogInformation(
                "Core automation cycle completed. Targets={TargetCount}, LiveFixtures={LiveFixtures}, HotLeagues={HotLeagues}, Actions={Actions}",
                currentTargets.Count,
                snapshot.LiveFixturesCount,
                snapshot.HotLeagueSeasons.Count,
                string.Join(" | ", actions));
        }

        var nextDelay = snapshot.ShouldUseActiveMode
            ? options.GetActiveInterval()
            : options.GetIdleInterval();

        return CoreDataAutomationCycleResult.Active(nextDelay, snapshot, actions);
    }

    private async Task RunCatalogRefreshJobAsync(
        CoreDataAutomationWorkerState state,
        DateTime nowUtc,
        CoreDataAutomationOptions options,
        Func<string, long?, int?, TimeSpan, bool> shouldSync,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        var forceCatalogRefresh = _coreLeagueCatalogState.GetTargets().Count == 0;
        var minimumAutomationSeason = nowUtc.Year - options.GetAutomationSeasonLookbackYears();
        var maximumAutomationSeason = nowUtc.Year + options.GetAutomationSeasonLookaheadYears();
        var dueActions = new Queue<string>();

        if (forceCatalogRefresh || shouldSync("countries", null, null, options.GetCatalogRefreshInterval()))
            dueActions.Enqueue("countries");

        if (forceCatalogRefresh || shouldSync("leagues_current", null, null, options.GetCatalogRefreshInterval()))
            dueActions.Enqueue("leagues_current");

        if (forceCatalogRefresh || shouldSync("leagues", null, null, options.GetCatalogRefreshInterval()))
            dueActions.Enqueue("leagues");

        if (forceCatalogRefresh || shouldSync("bookmakers_reference", null, null, options.GetBookmakersReferenceRefreshInterval()))
            dueActions.Enqueue("bookmakers_reference");

        var desiredRequests = dueActions.Count;
        if (desiredRequests == 0)
            return;

        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.CatalogRefresh,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.CatalogRefresh, "quota_budget_exhausted", desiredRequests);
            return;
        }

        var selected = dueActions
            .Take(allowedRequests)
            .ToHashSet(StringComparer.Ordinal);

        _quotaManager.MarkStarted(
            CoreAutomationJobNames.CatalogRefresh,
            desiredRequests,
            $"selected={string.Join(',', selected)}");

        var result = await _catalogRefreshJobService.RunAsync(
            nowUtc,
            minimumAutomationSeason,
            maximumAutomationSeason,
            refreshCountries: selected.Contains("countries"),
            refreshLeagues: selected.Contains("leagues"),
            refreshCurrentLeagues: selected.Contains("leagues_current"),
            refreshBookmakersReference: selected.Contains("bookmakers_reference"),
            registerSync,
            cancellationToken);

        if (selected.Contains("leagues_current"))
        {
            state.LastCatalogRunUtc = nowUtc;
        }

        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.CatalogRefresh,
            result.RequestsUsed,
            result.CurrentLeagueTargetsCount > 0 ? result.CurrentLeagueTargetsCount : result.Actions.Count,
            string.Join(" | ", result.Actions));

        actions.AddRange(result.Actions);
    }

    private async Task RunTeamsRollingJobAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> currentTargets,
        IReadOnlyDictionary<string, DateTime> syncLookup,
        CoreDataAutomationOptions options,
        Func<string, long?, int?, TimeSpan, bool> shouldSync,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        var quotaMode = GetApiQuotaSnapshot().Mode;
        var candidates = SelectDueTargets(
            currentTargets,
            "teams",
            options.GetTeamsInterval(),
            AdjustBatchSize(options.GetMaxTeamLeagueSeasonsPerCycle(), quotaMode, 4, 1),
            shouldSync,
            syncLookup);

        var desiredRequests = candidates.Count;
        if (desiredRequests == 0)
            return;

        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.TeamsRolling,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.TeamsRolling, "quota_budget_exhausted", desiredRequests);
            return;
        }

        var selectedTargets = candidates.Take(allowedRequests).ToList();
        _quotaManager.MarkStarted(
            CoreAutomationJobNames.TeamsRolling,
            desiredRequests,
            $"targets={selectedTargets.Count}");

        var result = await _teamsRollingJobService.RunAsync(selectedTargets, registerSync, cancellationToken);
        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.TeamsRolling,
            result.RequestsUsed,
            result.ProcessedItems,
            $"synced={result.SyncedKeys.Count}");

        if (result.SyncedKeys.Count > 0)
        {
            actions.Add($"teams:{string.Join(',', result.SyncedKeys)}");
        }
    }

    private async Task RunStandingsRollingJobAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> currentTargets,
        CoreDataAutomationSnapshot snapshot,
        IReadOnlyDictionary<string, DateTime> syncLookup,
        CoreDataAutomationOptions options,
        Func<string, long?, int?, TimeSpan, bool> shouldSync,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        var quotaMode = GetApiQuotaSnapshot().Mode;
        var standingsCapableKeys = currentTargets
            .Where(x => x.HasStandings)
            .Select(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season))
            .ToHashSet(StringComparer.Ordinal);

        var hotCandidates = SelectDueTargets(
            snapshot.HotLeagueSeasons.Where(x => standingsCapableKeys.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season))),
            "standings",
            options.GetStandingsHotInterval(),
            AdjustBatchSize(options.GetMaxStandingsLeagueSeasonsPerCycle(), quotaMode, 6, 2),
            shouldSync,
            syncLookup);

        var hotKeys = hotCandidates
            .Select(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season))
            .ToHashSet(StringComparer.Ordinal);

        var baselineCandidates = SelectDueTargets(
            currentTargets.Where(x => x.HasStandings && !hotKeys.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season))),
            "standings",
            options.GetStandingsInterval(),
            AdjustBatchSize(options.GetMaxStandingsLeagueSeasonsPerCycle(), quotaMode, 6, 2),
            shouldSync,
            syncLookup);

        var desiredRequests = hotCandidates.Count + baselineCandidates.Count;
        if (desiredRequests == 0)
            return;

        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.StandingsRolling,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.StandingsRolling, "quota_budget_exhausted", desiredRequests);
            return;
        }

        _quotaManager.MarkStarted(
            CoreAutomationJobNames.StandingsRolling,
            desiredRequests,
            $"hot={hotCandidates.Count},baseline={baselineCandidates.Count}");

        var selectedTargets = hotCandidates
            .Concat(baselineCandidates)
            .Take(allowedRequests)
            .ToList();

        var result = await _standingsRollingJobService.RunAsync(selectedTargets, registerSync, cancellationToken);

        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.StandingsRolling,
            result.RequestsUsed,
            result.ProcessedItems,
            $"synced={result.SyncedKeys.Count}");

        if (result.SyncedKeys.Count > 0)
        {
            actions.Add($"standings:{string.Join(',', result.SyncedKeys)}");
        }
    }

    private async Task<HashSet<string>> RunFixturesRollingJobAsync(
        CoreDataAutomationWorkerState state,
        CoreDataAutomationSnapshot snapshot,
        IReadOnlyDictionary<string, DateTime> syncLookup,
        DateTime nowUtc,
        CoreDataAutomationOptions options,
        Func<string, long?, int?, TimeSpan, bool> shouldSync,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        var quotaMode = GetApiQuotaSnapshot().Mode;
        var liveStatusDue = IsDue(state.LastLiveStatusRunUtc, nowUtc, options.GetLiveStatusInterval());

        var hotTargets = SelectDueTargets(
            snapshot.HotLeagueSeasons,
            "fixtures_hot",
            options.GetFixtureHotInterval(),
            AdjustBatchSize(options.GetMaxHotFixtureLeagueSeasonsPerCycle(), quotaMode, 4, 1),
            shouldSync,
            syncLookup);

        var baselineTargets = SelectDueTargets(
            _coreLeagueCatalogState.GetTargets().Where(x => x.HasFixtures),
            "fixtures_full",
            options.GetFixturesBaselineInterval(),
            AdjustBatchSize(options.GetMaxBaselineFixtureLeagueSeasonsPerCycle(), quotaMode, 4, 1),
            shouldSync,
            syncLookup);

        var desiredRequests = (liveStatusDue ? 1 : 0) + hotTargets.Count + baselineTargets.Count;
        if (desiredRequests == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.FixturesRolling,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.FixturesRolling, "quota_budget_exhausted", desiredRequests);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        _quotaManager.MarkStarted(
            CoreAutomationJobNames.FixturesRolling,
            desiredRequests,
            $"liveStatusDue={liveStatusDue},hot={hotTargets.Count},baseline={baselineTargets.Count}");

        var actualRequests = 0;
        var processedItems = 0;
        var hotFixtureKeys = new HashSet<string>(StringComparer.Ordinal);
        var remainingRequests = allowedRequests;

        if (liveStatusDue && remainingRequests > 0)
        {
            var liveStatusResult = await _fixturesRollingJobService.RunLiveStatusAsync(cancellationToken);
            state.LastLiveStatusRunUtc = nowUtc;
            remainingRequests -= liveStatusResult.RequestsUsed;
            actualRequests += liveStatusResult.RequestsUsed;
            processedItems += liveStatusResult.ProcessedItems;

            if (!string.IsNullOrWhiteSpace(liveStatusResult.Action))
            {
                actions.Add(liveStatusResult.Action);
            }
        }

        if (remainingRequests > 0 && hotTargets.Count > 0)
        {
            var hotResult = await _fixturesRollingJobService.RunRollingAsync(
                hotTargets.Take(remainingRequests).ToList(),
                "fixtures_hot",
                registerSync,
                cancellationToken);

            remainingRequests -= hotResult.RequestsUsed;
            actualRequests += hotResult.RequestsUsed;
            processedItems += hotResult.ProcessedItems;

            foreach (var key in hotResult.SyncedKeys)
            {
                hotFixtureKeys.Add(key);
            }

            if (hotResult.SyncedKeys.Count > 0)
            {
                actions.Add($"fixtures-hot:{string.Join(',', hotResult.SyncedKeys)}");
            }
        }

        if (remainingRequests > 0 && baselineTargets.Count > 0)
        {
            var baselineResult = await _fixturesRollingJobService.RunRollingAsync(
                baselineTargets.Take(remainingRequests).ToList(),
                "fixtures_full",
                registerSync,
                cancellationToken);

            actualRequests += baselineResult.RequestsUsed;
            processedItems += baselineResult.ProcessedItems;

            if (baselineResult.SyncedKeys.Count > 0)
            {
                actions.Add($"fixtures-baseline:{string.Join(',', baselineResult.SyncedKeys)}");
            }
        }

        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.FixturesRolling,
            actualRequests,
            processedItems,
            $"hot={hotFixtureKeys.Count}");

        return hotFixtureKeys;
    }

    private async Task RunOddsPreMatchJobAsync(
        CoreDataAutomationSnapshot snapshot,
        CoreDataAutomationOptions options,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        if (!options.EnablePreMatchOddsAutoSync)
            return;

        var quotaMode = GetApiQuotaSnapshot().Mode;
        if (quotaMode == ApiFootballQuotaMode.Critical)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.OddsPreMatch, "critical_provider_quota");
            return;
        }

        var candidates = await SelectDueOddsTargetsAsync(snapshot.OddsFixtures, options, quotaMode, cancellationToken);
        var desiredRequests = candidates.Count;
        if (desiredRequests == 0)
            return;

        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.OddsPreMatch,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.OddsPreMatch, "quota_budget_exhausted", desiredRequests);
            return;
        }

        var selectedTargets = candidates.Take(allowedRequests).ToList();
        _quotaManager.MarkStarted(
            CoreAutomationJobNames.OddsPreMatch,
            desiredRequests,
            $"fixtures={selectedTargets.Count}");

        var result = await _oddsPreMatchJobService.RunAsync(selectedTargets, registerSync, cancellationToken);
        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.OddsPreMatch,
            result.RequestsUsed,
            result.ProcessedItems,
            $"scopes={result.SyncedKeys.Count}");

        if (result.SyncedKeys.Count > 0)
        {
            actions.Add($"odds:{string.Join(',', result.SyncedKeys)}");
        }
    }

    private async Task RunOddsLiveJobAsync(
        CoreDataAutomationWorkerState state,
        CoreDataAutomationSnapshot snapshot,
        IReadOnlyDictionary<string, DateTime> syncLookup,
        DateTime nowUtc,
        CoreDataAutomationOptions options,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        if (!options.EnableLiveOddsAutoSync)
            return;

        var quotaMode = GetApiQuotaSnapshot().Mode;
        if (quotaMode == ApiFootballQuotaMode.Critical)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.OddsLive, "critical_provider_quota");
            return;
        }

        var betTypesDue = IsDue(state.LastLiveBetTypesRunUtc, nowUtc, options.GetLiveBetTypesRefreshInterval());
        var liveOddsDue = IsDue(state.LastLiveOddsRunUtc, nowUtc, options.GetLiveOddsInterval()) &&
                          snapshot.LiveLeagueSeasons.Count > 0;

        var maxLiveLeagues = AdjustBatchSize(options.GetMaxLiveOddsLeaguesPerCycle(), quotaMode, 3, 1);
        var distinctLiveLeagueCount = snapshot.LiveLeagueSeasons.Select(x => x.LeagueApiId).Distinct().Count();
        var desiredRequests = (betTypesDue ? 1 : 0) + (liveOddsDue ? Math.Min(distinctLiveLeagueCount, maxLiveLeagues) : 0);

        if (desiredRequests == 0)
            return;

        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.OddsLive,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.OddsLive, "quota_budget_exhausted", desiredRequests);
            return;
        }

        _quotaManager.MarkStarted(
            CoreAutomationJobNames.OddsLive,
            desiredRequests,
            $"betTypesDue={betTypesDue},liveLeagues={distinctLiveLeagueCount}");

        var remainingRequests = allowedRequests;
        var actualRequests = 0;
        var processedItems = 0;
        var syncedKeys = new List<string>();

        if (betTypesDue && remainingRequests > 0)
        {
            var betTypesResult = await _oddsLiveJobService.RunBetTypesAsync(cancellationToken);
            state.LastLiveBetTypesRunUtc = nowUtc;
            remainingRequests -= betTypesResult.RequestsUsed;
            actualRequests += betTypesResult.RequestsUsed;
            processedItems += betTypesResult.ProcessedItems;

            if (!string.IsNullOrWhiteSpace(betTypesResult.Action))
            {
                actions.Add(betTypesResult.Action);
            }
        }

        if (liveOddsDue && remainingRequests > 0)
        {
            var orderedLiveLeagueSeasons = snapshot.LiveLeagueSeasons
                .GroupBy(x => x.LeagueApiId)
                .OrderBy(x => x
                    .Select(y => GetLastSyncedAtUtc(syncLookup, "live_odds", y.LeagueApiId, y.Season) ?? DateTime.MinValue)
                    .Max())
                .ThenBy(x => x.Key)
                .SelectMany(x => x.OrderBy(y => y.Season))
                .ToList();

            var liveOddsResult = await _oddsLiveJobService.RunAsync(
                orderedLiveLeagueSeasons,
                remainingRequests,
                registerSync,
                cancellationToken);

            if (liveOddsResult.RequestsUsed > 0)
            {
                state.LastLiveOddsRunUtc = nowUtc;
            }

            actualRequests += liveOddsResult.RequestsUsed;
            processedItems += liveOddsResult.ProcessedItems;
            syncedKeys.AddRange(liveOddsResult.SyncedKeys);

            if (liveOddsResult.SyncedKeys.Count > 0)
            {
                actions.Add($"live-odds:{string.Join(',', liveOddsResult.SyncedKeys)}");
            }
        }

        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.OddsLive,
            actualRequests,
            processedItems,
            syncedKeys.Count > 0 ? $"scopes={syncedKeys.Count}" : "bet-types-only");
    }

    private async Task RunRepairJobAsync(
        CoreDataAutomationWorkerState state,
        CoreDataAutomationSnapshot snapshot,
        IReadOnlySet<string> hotFixtureKeys,
        IReadOnlyDictionary<string, DateTime> syncLookup,
        DateTime nowUtc,
        CoreDataAutomationOptions options,
        Func<string, long?, int?, TimeSpan, bool> shouldSync,
        Action<string, long?, int?> registerSync,
        List<string> actions,
        CancellationToken cancellationToken)
    {
        if (!IsDue(state.LastRepairRunUtc, nowUtc, options.GetRepairInterval()))
            return;

        var quotaMode = GetApiQuotaSnapshot().Mode;
        if (quotaMode == ApiFootballQuotaMode.Critical)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.Repair, "critical_provider_quota");
            return;
        }

        var repairTargets = snapshot.HotLeagueSeasons
            .Where(x => !hotFixtureKeys.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season)))
            .Where(x => shouldSync("fixtures_repair", x.LeagueApiId, x.Season, options.GetRepairInterval()))
            .OrderBy(x => GetLastSyncedAtUtc(syncLookup, "fixtures_repair", x.LeagueApiId, x.Season) ?? DateTime.MinValue)
            .ThenBy(x => x.LeagueApiId)
            .Take(options.GetMaxRepairLeagueSeasonsPerCycle())
            .ToList();

        if (repairTargets.Count == 0)
        {
            state.LastRepairRunUtc = nowUtc;
            return;
        }

        var desiredRequests = repairTargets.Count;
        var allowedRequests = _quotaManager.GetAllowedRequests(
            CoreAutomationJobNames.Repair,
            desiredRequests,
            options,
            GetApiQuotaSnapshot());

        if (allowedRequests <= 0)
        {
            _quotaManager.MarkSkipped(CoreAutomationJobNames.Repair, "quota_budget_exhausted", desiredRequests);
            return;
        }

        var selectedTargets = repairTargets.Take(allowedRequests).ToList();
        _quotaManager.MarkStarted(
            CoreAutomationJobNames.Repair,
            desiredRequests,
            $"targets={selectedTargets.Count}");

        var result = await _repairJobService.RunAsync(selectedTargets, registerSync, cancellationToken);
        state.LastRepairRunUtc = nowUtc;

        _quotaManager.MarkCompleted(
            CoreAutomationJobNames.Repair,
            result.RequestsUsed,
            result.ProcessedItems,
            $"synced={result.SyncedKeys.Count}");

        if (result.SyncedKeys.Count > 0)
        {
            actions.Add($"repair:{string.Join(',', result.SyncedKeys)}");
        }
    }

    private async Task<CoreDataAutomationSnapshot> BuildSnapshotAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> currentTargets,
        CoreDataAutomationOptions options,
        CancellationToken cancellationToken)
    {
        if (currentTargets.Count == 0)
            return new CoreDataAutomationSnapshot();

        var nowUtc = DateTime.UtcNow;
        var targetKeySet = currentTargets
            .Select(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season))
            .ToHashSet(StringComparer.Ordinal);

        var leagueIds = currentTargets.Select(x => x.LeagueApiId).Distinct().ToList();
        var seasons = currentTargets.Select(x => x.Season).Distinct().ToList();
        var liveStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Live).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var upcomingStatuses = FixtureStatusMapper.GetStatusesForBucket(FixtureStateBucket.Upcoming).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hotRangeStart = nowUtc.AddHours(-options.GetFixtureHotLookbackHours());
        var maxLookahead = nowUtc.AddHours(Math.Max(options.GetFixtureHotLookaheadHours(), options.GetOddsLookaheadHours()));
        var oddsTargetKeySet = currentTargets
            .Where(x => x.HasOdds)
            .Select(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season))
            .ToHashSet(StringComparer.Ordinal);

        var fixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x =>
                x.Status != null &&
                leagueIds.Contains(x.League.ApiLeagueId) &&
                seasons.Contains(x.Season) &&
                (liveStatuses.Contains(x.Status) || (x.KickoffAt >= hotRangeStart && x.KickoffAt <= maxLookahead)))
            .Select(x => new CoreAutomationFixtureCandidate
            {
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                Status = x.Status!,
                KickoffAt = x.KickoffAt
            })
            .ToListAsync(cancellationToken);

        fixtures = fixtures
            .Where(x => targetKeySet.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season)))
            .ToList();

        var hotRangeEnd = nowUtc.AddHours(options.GetFixtureHotLookaheadHours());
        var oddsRangeEnd = nowUtc.AddHours(options.GetOddsLookaheadHours());

        var hotLeagueSeasons = fixtures
            .Where(x => liveStatuses.Contains(x.Status) || (x.KickoffAt >= hotRangeStart && x.KickoffAt <= hotRangeEnd))
            .GroupBy(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season), StringComparer.Ordinal)
            .Select(x =>
            {
                var first = x.First();
                return new CoreLeagueSeasonTarget
                {
                    LeagueApiId = first.LeagueApiId,
                    Season = first.Season
                };
            })
            .OrderBy(x => x.LeagueApiId)
            .ThenBy(x => x.Season)
            .ToList();

        var liveLeagueSeasons = fixtures
            .Where(x => liveStatuses.Contains(x.Status))
            .GroupBy(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season), StringComparer.Ordinal)
            .Select(x =>
            {
                var first = x.First();
                return new CoreLeagueSeasonTarget
                {
                    LeagueApiId = first.LeagueApiId,
                    Season = first.Season
                };
            })
            .OrderBy(x => x.LeagueApiId)
            .ThenBy(x => x.Season)
            .ToList();

        var oddsFixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x =>
                leagueIds.Contains(x.League.ApiLeagueId) &&
                seasons.Contains(x.Season) &&
                x.Status != null &&
                upcomingStatuses.Contains(x.Status) &&
                x.KickoffAt >= nowUtc &&
                x.KickoffAt <= oddsRangeEnd)
            .Select(x => new CoreOddsFixtureTarget
            {
                ApiFixtureId = x.ApiFixtureId,
                LeagueApiId = x.League.ApiLeagueId,
                Season = x.Season,
                KickoffAtUtc = x.KickoffAt
            })
            .ToListAsync(cancellationToken);

        oddsFixtures = oddsFixtures
            .Where(x => oddsTargetKeySet.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season)))
            .ToList();

        return new CoreDataAutomationSnapshot
        {
            CurrentLeagueSeasonCount = currentTargets.Count,
            LiveFixturesCount = fixtures.Count(x => liveStatuses.Contains(x.Status)),
            HotLeagueSeasons = hotLeagueSeasons,
            LiveLeagueSeasons = liveLeagueSeasons,
            OddsFixtures = oddsFixtures
        };
    }

    private async Task<IReadOnlyList<CoreOddsFixtureTarget>> SelectDueOddsTargetsAsync(
        IEnumerable<CoreOddsFixtureTarget> candidates,
        CoreDataAutomationOptions options,
        ApiFootballQuotaMode quotaMode,
        CancellationToken cancellationToken)
    {
        var maxCount = AdjustBatchSize(options.GetMaxOddsFixturesPerCycle(), quotaMode, 6, 0);
        if (maxCount <= 0)
            return Array.Empty<CoreOddsFixtureTarget>();

        var nowUtc = DateTime.UtcNow;
        var apiFixtureIds = candidates.Select(x => x.ApiFixtureId).Distinct().ToList();
        if (apiFixtureIds.Count == 0)
            return Array.Empty<CoreOddsFixtureTarget>();

        var latestCollectedByApiFixtureId = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Where(x => apiFixtureIds.Contains(x.Fixture.ApiFixtureId))
            .GroupBy(x => x.Fixture.ApiFixtureId)
            .Select(x => new
            {
                ApiFixtureId = x.Key,
                LastCollectedAtUtc = x.Max(y => y.CollectedAt)
            })
            .ToDictionaryAsync(x => x.ApiFixtureId, x => x.LastCollectedAtUtc, cancellationToken);

        return candidates
            .Where(x =>
            {
                var interval = ResolveOddsInterval(nowUtc, x.KickoffAtUtc, options);
                return !latestCollectedByApiFixtureId.TryGetValue(x.ApiFixtureId, out var lastCollectedAtUtc) ||
                       nowUtc - lastCollectedAtUtc >= interval;
            })
            .OrderBy(x => x.KickoffAtUtc)
            .ThenBy(x => x.LeagueApiId)
            .Take(maxCount)
            .ToList();
    }

    private ApiFootballQuotaSnapshot GetApiQuotaSnapshot()
    {
        return _quotaTelemetryService.GetSnapshot(_apiFootballClientOptions.CurrentValue);
    }

    private static IReadOnlyList<CoreLeagueSeasonTarget> SelectDueTargets(
        IEnumerable<CoreLeagueSeasonTarget> candidates,
        string entityType,
        TimeSpan interval,
        int maxCount,
        Func<string, long?, int?, TimeSpan, bool> shouldSync,
        IReadOnlyDictionary<string, DateTime> syncLookup)
    {
        if (maxCount <= 0)
            return Array.Empty<CoreLeagueSeasonTarget>();

        return candidates
            .Where(x => shouldSync(entityType, x.LeagueApiId, x.Season, interval))
            .OrderBy(x => GetLastSyncedAtUtc(syncLookup, entityType, x.LeagueApiId, x.Season) ?? DateTime.MinValue)
            .ThenBy(x => x.LeagueApiId)
            .ThenBy(x => x.Season)
            .Take(maxCount)
            .ToList();
    }

    private static TimeSpan ResolveOddsInterval(
        DateTime nowUtc,
        DateTime nextKickoffAtUtc,
        CoreDataAutomationOptions options)
    {
        var timeUntilKickoff = nextKickoffAtUtc - nowUtc;

        if (timeUntilKickoff > TimeSpan.FromHours(options.GetOddsFarWindowHours()))
            return options.GetOddsFarInterval();

        if (timeUntilKickoff > TimeSpan.FromHours(options.GetOddsNearWindowHours()))
            return options.GetOddsMidInterval();

        if (timeUntilKickoff > TimeSpan.FromMinutes(options.GetOddsFinalWindowMinutes()))
            return options.GetOddsNearInterval();

        return options.GetOddsFinalInterval();
    }

    private static int AdjustBatchSize(
        int configured,
        ApiFootballQuotaMode mode,
        int lowValue,
        int criticalValue)
    {
        return mode switch
        {
            ApiFootballQuotaMode.Critical => Math.Min(configured, criticalValue),
            ApiFootballQuotaMode.Low => Math.Min(configured, lowValue),
            _ => configured
        };
    }

    private static bool IsDue(DateTime? lastRunUtc, DateTime nowUtc, TimeSpan interval)
    {
        return !lastRunUtc.HasValue || nowUtc - lastRunUtc.Value >= interval;
    }

    private static bool IsRecentlyUpdated(DateTime? lastRunUtc, DateTime nowUtc, TimeSpan interval)
    {
        return lastRunUtc.HasValue && nowUtc - lastRunUtc.Value <= interval;
    }

    private static DateTime? GetLastSyncedAtUtc(
        IReadOnlyDictionary<string, DateTime> syncLookup,
        string entityType,
        long? leagueApiId,
        int? season)
    {
        return syncLookup.TryGetValue(BuildSyncKey(entityType, leagueApiId, season), out var lastSyncedAt)
            ? lastSyncedAt
            : null;
    }

    private static string BuildLeagueSeasonKey(long leagueApiId, int season)
    {
        return $"{leagueApiId}:{season}";
    }

    private static string BuildSyncKey(string entityType, long? leagueApiId, int? season)
    {
        return $"{entityType}:{leagueApiId?.ToString() ?? "global"}:{season?.ToString() ?? "global"}";
    }

    private sealed class CoreAutomationFixtureCandidate
    {
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime KickoffAt { get; set; }
    }
}

public sealed class CoreDataAutomationWorkerState
{
    public DateTime? LastCatalogRunUtc { get; set; }
    public DateTime? LastLiveStatusRunUtc { get; set; }
    public DateTime? LastLiveBetTypesRunUtc { get; set; }
    public DateTime? LastLiveOddsRunUtc { get; set; }
    public DateTime? LastRepairRunUtc { get; set; }
}

public sealed class CoreDataAutomationCycleResult
{
    public string Mode { get; init; } = "idle";
    public TimeSpan NextDelay { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public CoreDataAutomationSnapshot Snapshot { get; init; } = new();

    public static CoreDataAutomationCycleResult Disabled(TimeSpan nextDelay)
    {
        return new CoreDataAutomationCycleResult
        {
            Mode = "disabled",
            NextDelay = nextDelay
        };
    }

    public static CoreDataAutomationCycleResult ConfigurationMissing(TimeSpan nextDelay)
    {
        return new CoreDataAutomationCycleResult
        {
            Mode = "missing-config",
            NextDelay = nextDelay
        };
    }

    public static CoreDataAutomationCycleResult Idle(
        TimeSpan nextDelay,
        CoreDataAutomationSnapshot snapshot,
        string reason)
    {
        return new CoreDataAutomationCycleResult
        {
            Mode = reason,
            NextDelay = nextDelay,
            Snapshot = snapshot
        };
    }

    public static CoreDataAutomationCycleResult Active(
        TimeSpan nextDelay,
        CoreDataAutomationSnapshot snapshot,
        IReadOnlyList<string> actions)
    {
        return new CoreDataAutomationCycleResult
        {
            Mode = "active",
            NextDelay = nextDelay,
            Snapshot = snapshot,
            Actions = actions
        };
    }
}

public sealed class CoreDataAutomationSnapshot
{
    public int CurrentLeagueSeasonCount { get; init; }
    public int LiveFixturesCount { get; init; }
    public IReadOnlyList<CoreLeagueSeasonTarget> HotLeagueSeasons { get; init; } = Array.Empty<CoreLeagueSeasonTarget>();
    public IReadOnlyList<CoreLeagueSeasonTarget> LiveLeagueSeasons { get; init; } = Array.Empty<CoreLeagueSeasonTarget>();
    public IReadOnlyList<CoreOddsFixtureTarget> OddsFixtures { get; init; } = Array.Empty<CoreOddsFixtureTarget>();

    public bool ShouldUseActiveMode => LiveFixturesCount > 0 || HotLeagueSeasons.Count > 0;
}

public sealed class CoreOddsFixtureTarget
{
    public long ApiFixtureId { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public DateTime KickoffAtUtc { get; set; }
}
