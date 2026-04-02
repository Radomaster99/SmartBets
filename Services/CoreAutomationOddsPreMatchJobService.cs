namespace SmartBets.Services;

public class CoreAutomationOddsPreMatchJobService
{
    private readonly PreMatchOddsService _preMatchOddsService;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationOddsPreMatchJobService> _logger;

    public CoreAutomationOddsPreMatchJobService(
        PreMatchOddsService preMatchOddsService,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationOddsPreMatchJobService> logger)
    {
        _preMatchOddsService = preMatchOddsService;
        _syncErrorService = syncErrorService;
        _logger = logger;
    }

    public async Task<CoreAutomationTargetJobResult> RunAsync(
        IReadOnlyList<CoreOddsFixtureTarget> targets,
        Action<string, long?, int?> registerSync,
        CancellationToken cancellationToken)
    {
        var result = new CoreAutomationTargetJobResult();

        if (targets.Count == 0)
            return result;

        try
        {
            var syncResult = await _preMatchOddsService.SyncOddsForFixturesAsync(
                targets.Select(x => x.ApiFixtureId).ToList(),
                cancellationToken: cancellationToken);

            foreach (var scope in syncResult.TouchedScopes)
            {
                registerSync("odds", scope.LeagueApiId, scope.Season);
                registerSync("bookmakers", scope.LeagueApiId, scope.Season);
                result.SyncedKeys.Add($"{scope.LeagueApiId}:{scope.Season}");
            }

            result.RequestsUsed = targets.Count;
            result.ProcessedItems = syncResult.SnapshotsInserted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pre-match odds rolling job failed.");
            await _syncErrorService.RecordAsync("odds", "background_sync", "core_automation", ex.Message, cancellationToken: cancellationToken);
        }

        return result;
    }
}
