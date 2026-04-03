using Microsoft.EntityFrameworkCore;
using SmartBets.Data;

namespace SmartBets.Services;

public class CoreAutomationOddsLiveJobService
{
    private readonly AppDbContext _dbContext;
    private readonly LiveOddsService _liveOddsService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationOddsLiveJobService> _logger;

    public CoreAutomationOddsLiveJobService(
        AppDbContext dbContext,
        LiveOddsService liveOddsService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationOddsLiveJobService> logger)
    {
        _dbContext = dbContext;
        _liveOddsService = liveOddsService;
        _syncErrorService = syncErrorService;
        _logger = logger;
    }

    public async Task<CoreAutomationSingleJobResult> RunBetTypesAsync(CancellationToken cancellationToken)
    {
        var result = await _liveOddsService.SyncLiveBetTypesAsync(cancellationToken);

        return new CoreAutomationSingleJobResult
        {
            RequestsUsed = 1,
            ProcessedItems = result.Processed,
            Action = $"live-bet-types:{result.Processed}"
        };
    }

    public async Task<int> GetTrackedBookmakerCountAsync(
        CoreDataAutomationOptions options,
        CancellationToken cancellationToken)
    {
        return (await GetTrackedBookmakerApiIdsAsync(options, cancellationToken)).Count;
    }

    public async Task<CoreAutomationTargetJobResult> RunAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> liveLeagueSeasons,
        int maxRequests,
        CoreDataAutomationOptions options,
        CoreDataAutomationWorkerState state,
        DateTime runStartedUtc,
        Action<string, long?, int?> registerSync,
        CancellationToken cancellationToken)
    {
        var result = new CoreAutomationTargetJobResult();
        if (maxRequests <= 0)
            return result;

        var distinctLeagueIds = new List<long>();
        var seenLeagueIds = new HashSet<long>();

        foreach (var liveLeagueSeason in liveLeagueSeasons)
        {
            if (!seenLeagueIds.Add(liveLeagueSeason.LeagueApiId))
                continue;

            distinctLeagueIds.Add(liveLeagueSeason.LeagueApiId);
        }

        if (distinctLeagueIds.Count == 0)
            return result;

        var trackedBookmakerApiIds = options.TrackLiveOddsPerBookmaker
            ? await GetTrackedBookmakerApiIdsAsync(options, cancellationToken)
            : Array.Empty<long>();

        if (options.TrackLiveOddsPerBookmaker && trackedBookmakerApiIds.Count == 0)
        {
            _logger.LogDebug("Live odds core job skipped because no tracked bookmakers are available.");
            return result;
        }

        var scopedSeasonLookup = liveLeagueSeasons
            .GroupBy(x => x.LeagueApiId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Season).Distinct().ToList());

        foreach (var leagueApiId in distinctLeagueIds)
        {
            if (result.RequestsUsed >= maxRequests)
                break;

            if (options.TrackLiveOddsPerBookmaker)
            {
                var anySucceeded = false;
                var dueBookmakerApiIds = trackedBookmakerApiIds
                    .OrderBy(x => state.LastLiveOddsRunByLeagueBookmakerKey.TryGetValue(
                        BuildLeagueBookmakerScopeKey(leagueApiId, x),
                        out var lastRunUtc)
                        ? lastRunUtc
                        : DateTime.MinValue)
                    .Take(options.GetMaxLiveOddsBookmakersPerLeaguePerCycle())
                    .ToList();

                foreach (var bookmakerApiId in dueBookmakerApiIds)
                {
                    if (result.RequestsUsed >= maxRequests)
                        break;

                    result.RequestsUsed++;

                    try
                    {
                        var syncResult = await _liveOddsService.SyncLiveOddsAsync(
                            leagueId: leagueApiId,
                            bookmakerId: bookmakerApiId,
                            cancellationToken: cancellationToken);

                        result.ProcessedItems += syncResult.SnapshotsInserted;
                        state.LastLiveOddsRunByLeagueBookmakerKey[BuildLeagueBookmakerScopeKey(leagueApiId, bookmakerApiId)] = runStartedUtc;
                        anySucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Live odds bookmaker job failed for league {LeagueApiId}, bookmaker {BookmakerApiId}.",
                            leagueApiId,
                            bookmakerApiId);

                        await _syncErrorService.RecordAsync(
                            "live_odds",
                            "background_sync",
                            "core_automation",
                            ex.Message,
                            leagueApiId,
                            cancellationToken: cancellationToken);
                    }
                }

                if (!anySucceeded || !scopedSeasonLookup.TryGetValue(leagueApiId, out var bookmakerSeasons))
                    continue;

                foreach (var season in bookmakerSeasons)
                {
                    registerSync("live_odds", leagueApiId, season);
                    AddSyncedKey(result.SyncedKeys, $"{leagueApiId}:{season}");
                }

                continue;
            }

            result.RequestsUsed++;

            try
            {
                var syncResult = await _liveOddsService.SyncLiveOddsAsync(
                    leagueId: leagueApiId,
                    cancellationToken: cancellationToken);

                result.ProcessedItems += syncResult.SnapshotsInserted;

                if (!scopedSeasonLookup.TryGetValue(leagueApiId, out var seasons))
                    continue;

                foreach (var season in seasons)
                {
                    registerSync("live_odds", leagueApiId, season);
                    AddSyncedKey(result.SyncedKeys, $"{leagueApiId}:{season}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live odds job failed for league {LeagueApiId}.", leagueApiId);
                await _syncErrorService.RecordAsync(
                    "live_odds",
                    "background_sync",
                    "core_automation",
                    ex.Message,
                    leagueApiId,
                    cancellationToken: cancellationToken);
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<long>> GetTrackedBookmakerApiIdsAsync(
        CoreDataAutomationOptions options,
        CancellationToken cancellationToken)
    {
        var configured = options.GetNormalizedLiveOddsBookmakerApiIds();
        if (configured.Count > 0)
            return configured;

        return await _dbContext.Bookmakers
            .AsNoTracking()
            .Where(x => x.ApiBookmakerId > 0)
            .OrderBy(x => x.Name)
            .Select(x => x.ApiBookmakerId)
            .ToListAsync(cancellationToken);
    }

    private static string BuildLeagueBookmakerScopeKey(long leagueApiId, long bookmakerApiId)
    {
        return $"{leagueApiId}:{bookmakerApiId}";
    }

    private static void AddSyncedKey(ICollection<string> syncedKeys, string key)
    {
        if (!syncedKeys.Contains(key, StringComparer.Ordinal))
        {
            syncedKeys.Add(key);
        }
    }
}
