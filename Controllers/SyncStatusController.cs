using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/sync-status")]
public class SyncStatusController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public SyncStatusController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var leagueKeys = supportedLeagues
            .Select(x => new { x.LeagueApiId, x.Season })
            .ToList();

        var leagueIds = leagueKeys.Select(x => x.LeagueApiId).Distinct().ToList();
        var seasons = leagueKeys.Select(x => x.Season).Distinct().ToList();

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
            }
        };

        var leagueStatuses = supportedLeagues
            .Select(x =>
            {
                leagueLookup.TryGetValue($"{x.LeagueApiId}:{x.Season}", out var league);

                return new LeagueSyncStatusItemDto
                {
                    LeagueApiId = x.LeagueApiId,
                    Season = x.Season,
                    LeagueName = league?.Name ?? string.Empty,
                    CountryName = league?.Country.Name ?? string.Empty,
                    IsActive = x.IsActive,
                    Priority = x.Priority,
                    TeamsLastSyncedAtUtc = GetLastSyncedAt("teams", x.LeagueApiId, x.Season),
                    FixturesUpcomingLastSyncedAtUtc = GetLastSyncedAt("fixtures_upcoming", x.LeagueApiId, x.Season),
                    FixturesFullLastSyncedAtUtc = GetLastSyncedAt("fixtures_full", x.LeagueApiId, x.Season),
                    StandingsLastSyncedAtUtc = GetLastSyncedAt("standings", x.LeagueApiId, x.Season),
                    OddsLastSyncedAtUtc = GetLastSyncedAt("odds", x.LeagueApiId, x.Season),
                    BookmakersLastSyncedAtUtc = GetLastSyncedAt("bookmakers", x.LeagueApiId, x.Season)
                };
            })
            .ToList();

        return Ok(new SyncStatusDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Global = global,
            Leagues = leagueStatuses
        });
    }
}
