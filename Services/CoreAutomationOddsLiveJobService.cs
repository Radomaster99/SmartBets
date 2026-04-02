namespace SmartBets.Services;

public class CoreAutomationOddsLiveJobService
{
    private readonly LiveOddsService _liveOddsService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationOddsLiveJobService> _logger;

    public CoreAutomationOddsLiveJobService(
        LiveOddsService liveOddsService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationOddsLiveJobService> logger)
    {
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

    public async Task<CoreAutomationTargetJobResult> RunAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> liveLeagueSeasons,
        int maxLeaguesPerCycle,
        Action<string, long?, int?> registerSync,
        CancellationToken cancellationToken)
    {
        var result = new CoreAutomationTargetJobResult();

        var distinctLeagueIds = liveLeagueSeasons
            .Select(x => x.LeagueApiId)
            .Distinct()
            .OrderBy(x => x)
            .Take(Math.Max(0, maxLeaguesPerCycle))
            .ToList();

        if (distinctLeagueIds.Count == 0)
            return result;

        var scopedSeasonLookup = liveLeagueSeasons
            .GroupBy(x => x.LeagueApiId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Season).Distinct().ToList());

        foreach (var leagueApiId in distinctLeagueIds)
        {
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
                    result.SyncedKeys.Add($"{leagueApiId}:{season}");
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
}
