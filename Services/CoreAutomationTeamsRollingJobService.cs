namespace SmartBets.Services;

public class CoreAutomationTeamsRollingJobService
{
    private readonly TeamSyncService _teamSyncService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationTeamsRollingJobService> _logger;

    public CoreAutomationTeamsRollingJobService(
        TeamSyncService teamSyncService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationTeamsRollingJobService> logger)
    {
        _teamSyncService = teamSyncService;
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
                var syncResult = await _teamSyncService.SyncTeamsAsync(target.LeagueApiId, target.Season, cancellationToken);
                registerSync("teams", target.LeagueApiId, target.Season);
                result.SyncedKeys.Add($"{target.LeagueApiId}:{target.Season}");
                result.ProcessedItems += syncResult.Processed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Teams rolling job failed for league {LeagueApiId}, season {Season}.", target.LeagueApiId, target.Season);
                await _syncErrorService.RecordAsync("teams", "background_sync", "core_automation", ex.Message, target.LeagueApiId, target.Season, cancellationToken);
            }
        }

        return result;
    }
}
