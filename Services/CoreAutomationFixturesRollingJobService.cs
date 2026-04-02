namespace SmartBets.Services;

public class CoreAutomationFixturesRollingJobService
{
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly FixtureLiveStatusSyncService _fixtureLiveStatusSyncService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationFixturesRollingJobService> _logger;

    public CoreAutomationFixturesRollingJobService(
        FixtureSyncService fixtureSyncService,
        FixtureLiveStatusSyncService fixtureLiveStatusSyncService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationFixturesRollingJobService> logger)
    {
        _fixtureSyncService = fixtureSyncService;
        _fixtureLiveStatusSyncService = fixtureLiveStatusSyncService;
        _syncErrorService = syncErrorService;
        _logger = logger;
    }

    public async Task<CoreAutomationSingleJobResult> RunLiveStatusAsync(CancellationToken cancellationToken)
    {
        var liveStatusResult = await _fixtureLiveStatusSyncService.SyncLiveFixturesAsync(
            activeOnly: false,
            cancellationToken: cancellationToken);

        return new CoreAutomationSingleJobResult
        {
            RequestsUsed = liveStatusResult.RequestsUsed,
            ProcessedItems = liveStatusResult.FixturesProcessed,
            Action = $"fixtures-live:{liveStatusResult.FixturesProcessed}"
        };
    }

    public async Task<CoreAutomationTargetJobResult> RunRollingAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> targets,
        string syncEntityType,
        Action<string, long?, int?> registerSync,
        CancellationToken cancellationToken)
    {
        var result = new CoreAutomationTargetJobResult();

        foreach (var target in targets)
        {
            result.RequestsUsed++;

            try
            {
                var syncResult = await _fixtureSyncService.SyncFixturesAsync(target.LeagueApiId, target.Season, cancellationToken);
                registerSync(syncEntityType, target.LeagueApiId, target.Season);
                registerSync("fixtures_full", target.LeagueApiId, target.Season);
                result.SyncedKeys.Add($"{target.LeagueApiId}:{target.Season}");
                result.ProcessedItems += syncResult.Processed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fixtures rolling job failed for {EntityType}, league {LeagueApiId}, season {Season}.", syncEntityType, target.LeagueApiId, target.Season);
                await _syncErrorService.RecordAsync(syncEntityType, "background_sync", "core_automation", ex.Message, target.LeagueApiId, target.Season, cancellationToken);
            }
        }

        return result;
    }
}
