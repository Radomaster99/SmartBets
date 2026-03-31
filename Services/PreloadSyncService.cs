using Microsoft.EntityFrameworkCore;
using SmartBets.Data;

namespace SmartBets.Services;

public class PreloadSyncLeagueResult
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }

    public int TeamsProcessed { get; set; }
    public int TeamsInserted { get; set; }
    public int TeamsUpdated { get; set; }

    public int FixturesProcessed { get; set; }
    public int FixturesInserted { get; set; }
    public int FixturesUpdated { get; set; }
    public int FixturesSkippedMissingTeams { get; set; }

    public int StandingsProcessed { get; set; }
    public int StandingsInserted { get; set; }
    public int StandingsUpdated { get; set; }
    public int StandingsSkippedMissingTeams { get; set; }

    public string Status { get; set; } = "Success";
    public string? Error { get; set; }
}

public class PreloadSyncResult
{
    public bool CountriesSynced { get; set; }
    public bool LeaguesSynced { get; set; }
    public int SupportedLeaguesCount { get; set; }
    public List<PreloadSyncLeagueResult> Leagues { get; set; } = new();
}

public class PreloadSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly CountrySyncService _countrySyncService;
    private readonly LeagueSyncService _leagueSyncService;
    private readonly TeamSyncService _teamSyncService;
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly StandingsSyncService _standingsSyncService;
    private readonly SyncStateService _syncStateService;

    public PreloadSyncService(
        AppDbContext dbContext,
        CountrySyncService countrySyncService,
        LeagueSyncService leagueSyncService,
        TeamSyncService teamSyncService,
        FixtureSyncService fixtureSyncService,
        StandingsSyncService standingsSyncService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
        _countrySyncService = countrySyncService;
        _leagueSyncService = leagueSyncService;
        _teamSyncService = teamSyncService;
        _fixtureSyncService = fixtureSyncService;
        _standingsSyncService = standingsSyncService;
        _syncStateService = syncStateService;
    }

    public async Task<PreloadSyncResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var result = new PreloadSyncResult();

        // 1. Countries
        await _countrySyncService.SyncCountriesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("countries", null, null, nowUtc, cancellationToken);
        result.CountriesSynced = true;

        // 2. Leagues
        await _leagueSyncService.SyncLeaguesAsync(cancellationToken);
        await _syncStateService.SetLastSyncedAtAsync("leagues", null, null, nowUtc, cancellationToken);
        result.LeaguesSynced = true;

        // 3. Supported leagues
        var supportedLeagues = await _dbContext.SupportedLeagues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .ToListAsync(cancellationToken);

        result.SupportedLeaguesCount = supportedLeagues.Count;

        foreach (var supportedLeague in supportedLeagues)
        {
            var leagueResult = new PreloadSyncLeagueResult
            {
                LeagueApiId = supportedLeague.LeagueApiId,
                Season = supportedLeague.Season
            };

            try
            {
                var leagueExists = await _dbContext.Leagues
                    .AnyAsync(
                        x => x.ApiLeagueId == supportedLeague.LeagueApiId &&
                             x.Season == supportedLeague.Season,
                        cancellationToken);

                if (!leagueExists)
                {
                    leagueResult.Status = "Failed";
                    leagueResult.Error =
                        $"League with apiLeagueId {supportedLeague.LeagueApiId} and season {supportedLeague.Season} was not found in database after leagues sync.";

                    result.Leagues.Add(leagueResult);
                    continue;
                }

                // Teams
                var teamResult = await _teamSyncService.SyncTeamsAsync(
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    cancellationToken);

                leagueResult.TeamsProcessed = teamResult.Processed;
                leagueResult.TeamsInserted = teamResult.Inserted;
                leagueResult.TeamsUpdated = teamResult.Updated;

                await _syncStateService.SetLastSyncedAtAsync(
                    "teams",
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    nowUtc,
                    cancellationToken);

                // Upcoming fixtures
                var fixtureResult = await _fixtureSyncService.SyncUpcomingFixturesAsync(
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    cancellationToken);

                leagueResult.FixturesProcessed = fixtureResult.Processed;
                leagueResult.FixturesInserted = fixtureResult.Inserted;
                leagueResult.FixturesUpdated = fixtureResult.Updated;
                leagueResult.FixturesSkippedMissingTeams = fixtureResult.SkippedMissingTeams;

                await _syncStateService.SetLastSyncedAtAsync(
                    "fixtures_upcoming",
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    nowUtc,
                    cancellationToken);

                // Standings
                var standingsResult = await _standingsSyncService.SyncStandingsAsync(
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    cancellationToken);

                leagueResult.StandingsProcessed = standingsResult.Processed;
                leagueResult.StandingsInserted = standingsResult.Inserted;
                leagueResult.StandingsUpdated = standingsResult.Updated;
                leagueResult.StandingsSkippedMissingTeams = standingsResult.SkippedMissingTeams;

                await _syncStateService.SetLastSyncedAtAsync(
                    "standings",
                    supportedLeague.LeagueApiId,
                    supportedLeague.Season,
                    nowUtc,
                    cancellationToken);

                leagueResult.Status = "Success";
            }
            catch (Exception ex)
            {
                leagueResult.Status = "Failed";
                leagueResult.Error = ex.Message;
            }

            result.Leagues.Add(leagueResult);
        }

        return result;
    }
}