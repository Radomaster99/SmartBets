namespace SmartBets.Services;

public class CoreAutomationRepairJobService
{
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationRepairJobService> _logger;

    public CoreAutomationRepairJobService(
        FixtureSyncService fixtureSyncService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationRepairJobService> logger)
    {
        _fixtureSyncService = fixtureSyncService;
        _syncErrorService = syncErrorService;
        _logger = logger;
    }

    public async Task<CoreAutomationTargetJobResult> RunAsync(
        IReadOnlyList<CoreLeagueSeasonTarget> targets,
        Action<string, long?, int?> registerSync,
        CancellationToken cancellationToken)
    {
        var result = new CoreAutomationTargetJobResult();

        foreach (var target in targets)
        {
            result.RequestsUsed++;

            try
            {
                await _fixtureSyncService.SyncFixturesAsync(target.LeagueApiId, target.Season, cancellationToken);
                registerSync("fixtures_repair", target.LeagueApiId, target.Season);
                registerSync("fixtures_full", target.LeagueApiId, target.Season);
                result.SyncedKeys.Add($"{target.LeagueApiId}:{target.Season}");
                result.ProcessedItems++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Repair job failed for league {LeagueApiId}, season {Season}.", target.LeagueApiId, target.Season);
                await _syncErrorService.RecordAsync(
                    "fixtures_repair",
                    "background_sync",
                    "core_automation",
                    ex.Message,
                    target.LeagueApiId,
                    target.Season,
                    cancellationToken);
            }
        }

        return result;
    }
}
