namespace SmartBets.Services;

public class CoreAutomationCatalogRefreshJobService
{
    private readonly CountrySyncService _countrySyncService;
    private readonly LeagueSyncService _leagueSyncService;
    private readonly BookmakerSyncService _bookmakerSyncService;
    private readonly CoreLeagueCatalogState _coreLeagueCatalogState;
    private readonly SyncErrorService _syncErrorService;
    private readonly ILogger<CoreAutomationCatalogRefreshJobService> _logger;

    public CoreAutomationCatalogRefreshJobService(
        CountrySyncService countrySyncService,
        LeagueSyncService leagueSyncService,
        BookmakerSyncService bookmakerSyncService,
        CoreLeagueCatalogState coreLeagueCatalogState,
        SyncErrorService syncErrorService,
        ILogger<CoreAutomationCatalogRefreshJobService> logger)
    {
        _countrySyncService = countrySyncService;
        _leagueSyncService = leagueSyncService;
        _bookmakerSyncService = bookmakerSyncService;
        _coreLeagueCatalogState = coreLeagueCatalogState;
        _syncErrorService = syncErrorService;
        _logger = logger;
    }

    public async Task<CoreAutomationCatalogRefreshJobResult> RunAsync(
        DateTime nowUtc,
        int minimumAutomationSeason,
        int maximumAutomationSeason,
        bool refreshCountries,
        bool refreshLeagues,
        bool refreshCurrentLeagues,
        bool refreshBookmakersReference,
        Action<string, long?, int?> registerSync,
        CancellationToken cancellationToken)
    {
        var result = new CoreAutomationCatalogRefreshJobResult();

        if (refreshCountries)
        {
            result.RequestsUsed++;

            try
            {
                await _countrySyncService.SyncCountriesAsync(cancellationToken);
                registerSync("countries", null, null);
                result.Actions.Add("countries");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync("countries", ex, result, cancellationToken);
            }
        }

        if (refreshLeagues)
        {
            result.RequestsUsed++;

            try
            {
                var processed = await _leagueSyncService.SyncLeaguesAsync(cancellationToken);
                registerSync("leagues", null, null);
                result.Actions.Add($"leagues:{processed}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync("leagues", ex, result, cancellationToken);
            }
        }

        if (refreshCurrentLeagues)
        {
            result.RequestsUsed++;

            try
            {
                var currentTargets = await _leagueSyncService.SyncCurrentLeaguesAsync(
                    minimumAutomationSeason,
                    maximumAutomationSeason,
                    cancellationToken);
                if (currentTargets.Count > 0)
                {
                    _coreLeagueCatalogState.ReplaceTargets(currentTargets, nowUtc);
                    result.CurrentLeagueTargetsCount = currentTargets.Count;
                }

                registerSync("leagues_current", null, null);
                result.Actions.Add($"leagues-current:{currentTargets.Count}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync("leagues_current", ex, result, cancellationToken);
            }
        }

        if (refreshBookmakersReference)
        {
            result.RequestsUsed++;

            try
            {
                var bookmakers = await _bookmakerSyncService.SyncReferenceBookmakersAsync(cancellationToken);
                registerSync("bookmakers_reference", null, null);
                result.Actions.Add($"bookmakers-reference:{bookmakers.Processed}");
                result.RequestsUsed += Math.Max(0, bookmakers.RemoteCallsMade - 1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync("bookmakers_reference", ex, result, cancellationToken);
            }
        }

        return result;
    }

    private async Task RecordFailureAsync(
        string entityType,
        Exception exception,
        CoreAutomationCatalogRefreshJobResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Catalog refresh failed for {EntityType}. Continuing with remaining automation steps.", entityType);
        result.Actions.Add($"{entityType}:failed");

        await _syncErrorService.RecordAsync(
            entityType,
            "background_sync",
            "core_automation",
            exception.Message,
            cancellationToken: cancellationToken);
    }
}

public class CoreAutomationCatalogRefreshJobResult
{
    public int RequestsUsed { get; set; }
    public int CurrentLeagueTargetsCount { get; set; }
    public List<string> Actions { get; } = new();
}
