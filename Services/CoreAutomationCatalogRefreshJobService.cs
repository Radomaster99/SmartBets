namespace SmartBets.Services;

public class CoreAutomationCatalogRefreshJobService
{
    private readonly CountrySyncService _countrySyncService;
    private readonly LeagueSyncService _leagueSyncService;
    private readonly BookmakerSyncService _bookmakerSyncService;
    private readonly CoreLeagueCatalogState _coreLeagueCatalogState;

    public CoreAutomationCatalogRefreshJobService(
        CountrySyncService countrySyncService,
        LeagueSyncService leagueSyncService,
        BookmakerSyncService bookmakerSyncService,
        CoreLeagueCatalogState coreLeagueCatalogState)
    {
        _countrySyncService = countrySyncService;
        _leagueSyncService = leagueSyncService;
        _bookmakerSyncService = bookmakerSyncService;
        _coreLeagueCatalogState = coreLeagueCatalogState;
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
            await _countrySyncService.SyncCountriesAsync(cancellationToken);
            registerSync("countries", null, null);
            result.Actions.Add("countries");
            result.RequestsUsed++;
        }

        if (refreshLeagues)
        {
            var processed = await _leagueSyncService.SyncLeaguesAsync(cancellationToken);
            registerSync("leagues", null, null);
            result.Actions.Add($"leagues:{processed}");
            result.RequestsUsed++;
        }

        if (refreshCurrentLeagues)
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
            result.RequestsUsed++;
        }

        if (refreshBookmakersReference)
        {
            var bookmakers = await _bookmakerSyncService.SyncReferenceBookmakersAsync(cancellationToken);
            registerSync("bookmakers_reference", null, null);
            result.Actions.Add($"bookmakers-reference:{bookmakers.Processed}");
            result.RequestsUsed += Math.Max(1, bookmakers.RemoteCallsMade);
        }

        return result;
    }
}

public class CoreAutomationCatalogRefreshJobResult
{
    public int RequestsUsed { get; set; }
    public int CurrentLeagueTargetsCount { get; set; }
    public List<string> Actions { get; } = new();
}
