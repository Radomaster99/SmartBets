using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/sync-status")]
public class SyncStatusController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly CoreLeagueCatalogState _coreLeagueCatalogState;
    private readonly ApiFootballQuotaTelemetryService _quotaTelemetryService;
    private readonly IOptionsMonitor<ApiFootballClientOptions> _apiFootballClientOptions;
    private readonly CoreAutomationQuotaManager _coreAutomationQuotaManager;
    private readonly IOptionsMonitor<CoreDataAutomationOptions> _coreDataAutomationOptions;

    public SyncStatusController(
        AppDbContext dbContext,
        CoreLeagueCatalogState coreLeagueCatalogState,
        ApiFootballQuotaTelemetryService quotaTelemetryService,
        IOptionsMonitor<ApiFootballClientOptions> apiFootballClientOptions,
        CoreAutomationQuotaManager coreAutomationQuotaManager,
        IOptionsMonitor<CoreDataAutomationOptions> coreDataAutomationOptions)
    {
        _dbContext = dbContext;
        _coreLeagueCatalogState = coreLeagueCatalogState;
        _quotaTelemetryService = quotaTelemetryService;
        _apiFootballClientOptions = apiFootballClientOptions;
        _coreAutomationQuotaManager = coreAutomationQuotaManager;
        _coreDataAutomationOptions = coreDataAutomationOptions;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? season,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var syncStates = await _dbContext.SyncStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var stateLookup = syncStates.ToDictionary(
            x => $"{x.EntityType}:{x.LeagueApiId?.ToString() ?? "global"}:{x.Season?.ToString() ?? "global"}",
            x => x.LastSyncedAt);

        var supportedLeaguesQuery = _dbContext.SupportedLeagues
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Where(x => x.IsActive);
        }

        if (season.HasValue)
        {
            supportedLeaguesQuery = supportedLeaguesQuery.Where(x => x.Season == season.Value);
        }

        var supportedLeagues = await supportedLeaguesQuery
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .ToListAsync(cancellationToken);

        var supportedLookup = supportedLeagues.ToDictionary(
            x => $"{x.LeagueApiId}:{x.Season}",
            x => x,
            StringComparer.Ordinal);

        var coreTargets = _coreLeagueCatalogState.GetTargets()
            .Where(x => !season.HasValue || x.Season == season.Value)
            .ToList();

        var targetLeagueKeys = coreTargets.Count > 0
            ? coreTargets.Select(x => new { x.LeagueApiId, x.Season }).ToList()
            : supportedLeagues.Select(x => new { x.LeagueApiId, x.Season }).ToList();

        var leagueIds = targetLeagueKeys.Select(x => x.LeagueApiId).Distinct().ToList();
        var seasons = targetLeagueKeys.Select(x => x.Season).Distinct().ToList();

        var leagues = await _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => leagueIds.Contains(x.ApiLeagueId) && seasons.Contains(x.Season))
            .ToListAsync(cancellationToken);

        var leagueLookup = leagues.ToDictionary(
            x => $"{x.ApiLeagueId}:{x.Season}",
            x => x);

        DateTime? GetLastSyncedAt(string entityType, long? leagueApiId, int? entitySeason)
        {
            var key = $"{entityType}:{leagueApiId?.ToString() ?? "global"}:{entitySeason?.ToString() ?? "global"}";
            return stateLookup.TryGetValue(key, out var lastSyncedAt) ? lastSyncedAt : null;
        }

        var global = new List<GlobalSyncStatusItemDto>
        {
            new()
            {
                EntityType = "countries",
                LastSyncedAtUtc = GetLastSyncedAt("countries", null, null)
            },
            new()
            {
                EntityType = "leagues",
                LastSyncedAtUtc = GetLastSyncedAt("leagues", null, null)
            },
            new()
            {
                EntityType = "leagues_current",
                LastSyncedAtUtc = GetLastSyncedAt("leagues_current", null, null)
            },
            new()
            {
                EntityType = "live_bet_types",
                LastSyncedAtUtc = GetLastSyncedAt("live_bet_types", null, null)
            },
            new()
            {
                EntityType = "bookmakers_reference",
                LastSyncedAtUtc = GetLastSyncedAt("bookmakers_reference", null, null)
            }
        };

        var leagueStatuses = (coreTargets.Count > 0 ? coreTargets.Select(x => new { x.LeagueApiId, x.Season }) : supportedLeagues.Select(x => new { x.LeagueApiId, x.Season }))
            .Select(x =>
            {
                leagueLookup.TryGetValue($"{x.LeagueApiId}:{x.Season}", out var league);
                supportedLookup.TryGetValue($"{x.LeagueApiId}:{x.Season}", out var supportedLeague);

                return new LeagueSyncStatusItemDto
                {
                    LeagueApiId = x.LeagueApiId,
                    Season = x.Season,
                    LeagueName = league?.Name ?? string.Empty,
                    CountryName = league?.Country.Name ?? string.Empty,
                    IsActive = supportedLeague?.IsActive ?? false,
                    Priority = supportedLeague?.Priority ?? 0,
                    TeamsLastSyncedAtUtc = GetLastSyncedAt("teams", x.LeagueApiId, x.Season),
                    FixturesLiveLastSyncedAtUtc = GetLastSyncedAt("fixtures_live", x.LeagueApiId, x.Season),
                    FixturesUpcomingLastSyncedAtUtc = GetLastSyncedAt("fixtures_upcoming", x.LeagueApiId, x.Season),
                    FixturesFullLastSyncedAtUtc = GetLastSyncedAt("fixtures_full", x.LeagueApiId, x.Season),
                    EventsLastSyncedAtUtc = GetLastSyncedAt("fixture_events", x.LeagueApiId, x.Season),
                    StatisticsLastSyncedAtUtc = GetLastSyncedAt("fixture_statistics", x.LeagueApiId, x.Season),
                    LineupsLastSyncedAtUtc = GetLastSyncedAt("fixture_lineups", x.LeagueApiId, x.Season),
                    PlayerStatisticsLastSyncedAtUtc = GetLastSyncedAt("fixture_player_statistics", x.LeagueApiId, x.Season),
                    PredictionsLastSyncedAtUtc = GetLastSyncedAt("fixture_predictions", x.LeagueApiId, x.Season),
                    InjuriesLastSyncedAtUtc = GetLastSyncedAt("fixture_injuries", x.LeagueApiId, x.Season),
                    TeamStatisticsLastSyncedAtUtc = GetLastSyncedAt("team_statistics", x.LeagueApiId, x.Season),
                    RoundsLastSyncedAtUtc = GetLastSyncedAt("league_rounds", x.LeagueApiId, x.Season),
                    TopScorersLastSyncedAtUtc = GetLastSyncedAt("league_top_scorers", x.LeagueApiId, x.Season),
                    TopAssistsLastSyncedAtUtc = GetLastSyncedAt("league_top_assists", x.LeagueApiId, x.Season),
                    TopCardsLastSyncedAtUtc = GetLastSyncedAt("league_top_cards", x.LeagueApiId, x.Season),
                    StandingsLastSyncedAtUtc = GetLastSyncedAt("standings", x.LeagueApiId, x.Season),
                    OddsLastSyncedAtUtc = GetLastSyncedAt("odds", x.LeagueApiId, x.Season),
                    LiveOddsLastSyncedAtUtc = GetLastSyncedAt("live_odds", x.LeagueApiId, x.Season),
                    OddsAnalyticsLastSyncedAtUtc = GetLastSyncedAt("odds_analytics", x.LeagueApiId, x.Season),
                    BookmakersLastSyncedAtUtc = GetLastSyncedAt("bookmakers", x.LeagueApiId, x.Season)
                };
            })
            .ToList();

        return Ok(new SyncStatusDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            ApiQuota = MapQuotaStatus(),
            CoreAutomation = MapCoreAutomationStatus(coreTargets.Count),
            Global = global,
            Leagues = leagueStatuses
        });
    }

    private ApiQuotaStatusDto MapQuotaStatus()
    {
        var snapshot = _quotaTelemetryService.GetSnapshot(_apiFootballClientOptions.CurrentValue);

        return new ApiQuotaStatusDto
        {
            Provider = "api-football",
            Mode = snapshot.Mode.ToString(),
            RequestsDailyLimit = snapshot.RequestsDailyLimit,
            RequestsDailyRemaining = snapshot.RequestsDailyRemaining,
            RequestsMinuteLimit = snapshot.RequestsMinuteLimit,
            RequestsMinuteRemaining = snapshot.RequestsMinuteRemaining,
            LastObservedAtUtc = snapshot.LastObservedAtUtc
        };
    }

    private CoreAutomationStatusDto MapCoreAutomationStatus(int currentLeagueSeasonCount)
    {
        var snapshot = _coreAutomationQuotaManager.GetSnapshot(_coreDataAutomationOptions.CurrentValue);

        return new CoreAutomationStatusDto
        {
            CatalogLastRefreshedAtUtc = _coreLeagueCatalogState.GetLastRefreshedAtUtc(),
            CurrentLeagueSeasonCount = currentLeagueSeasonCount,
            DailyBudget = snapshot.DailyBudget,
            UsedToday = snapshot.UsedToday,
            RemainingToday = snapshot.RemainingToday,
            Jobs = snapshot.Jobs
                .Select(x => new CoreAutomationJobStatusDto
                {
                    Job = x.Job,
                    DailyBudget = x.DailyBudget,
                    UsedToday = x.UsedToday,
                    RemainingToday = x.RemainingToday,
                    LastStartedAtUtc = x.LastStartedAtUtc,
                    LastCompletedAtUtc = x.LastCompletedAtUtc,
                    LastSkippedAtUtc = x.LastSkippedAtUtc,
                    LastStatus = x.LastStatus,
                    LastReason = x.LastReason,
                    LastDesiredRequests = x.LastDesiredRequests,
                    LastActualRequests = x.LastActualRequests,
                    LastProcessedItems = x.LastProcessedItems
                })
                .ToList()
        };
    }
}
