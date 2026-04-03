namespace SmartBets.Services;

public class CoreAutomationStandingsRollingJobService
{
    private readonly StandingsSyncService _standingsSyncService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationStandingsRollingJobService> _logger;

    public CoreAutomationStandingsRollingJobService(
        StandingsSyncService standingsSyncService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationStandingsRollingJobService> logger)
    {
        _standingsSyncService = standingsSyncService;
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
                var syncResult = await _standingsSyncService.SyncStandingsAsync(
                    target.LeagueApiId,
                    target.Season,
                    cancellationToken);

                registerSync("standings", target.LeagueApiId, target.Season);
                result.SyncedKeys.Add($"{target.LeagueApiId}:{target.Season}");
                result.ProcessedItems += syncResult.Processed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Standings rolling job failed for league {LeagueApiId}, season {Season}.",
                    target.LeagueApiId,
                    target.Season);

                await _syncErrorService.RecordAsync(
                    "standings",
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
