using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly TeamSyncService _teamSyncService;
    private readonly TeamAnalyticsService _teamAnalyticsService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public TeamsController(
        TeamSyncService teamSyncService,
        TeamAnalyticsService teamAnalyticsService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _teamSyncService = teamSyncService;
        _teamAnalyticsService = teamAnalyticsService;
        _dbContext = dbContext;
        _syncStateService = syncStateService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] int season,
        [FromQuery] long? leagueId,
        [FromQuery] int maxLeagues = 5,
        CancellationToken cancellationToken = default)
    {
        if (maxLeagues <= 0)
            return BadRequest("maxLeagues must be greater than 0.");

        var syncedAtUtc = DateTime.UtcNow;

        if (leagueId.HasValue)
        {
            var result = await _teamSyncService.SyncTeamsAsync(
                leagueId.Value,
                season,
                cancellationToken);

            await _syncStateService.SetLastSyncedAtAsync(
                "teams",
                leagueId.Value,
                season,
                syncedAtUtc,
                cancellationToken);

            return Ok(new
            {
                Message = "Teams synced for specific league.",
                LeagueId = leagueId,
                Season = season,
                LastSyncedAtUtc = syncedAtUtc,
                result.Processed,
                result.Inserted,
                result.Updated
            });
        }

        var leagues = await _dbContext.Leagues
            .AsNoTracking()
            .Where(x => x.Season == season)
            .OrderBy(x => x.ApiLeagueId)
            .Take(maxLeagues)
            .ToListAsync(cancellationToken);

        var totalProcessed = 0;
        var totalInserted = 0;
        var totalUpdated = 0;

        foreach (var league in leagues)
        {
            var result = await _teamSyncService.SyncTeamsAsync(
                league.ApiLeagueId,
                season,
                cancellationToken);

            await _syncStateService.SetLastSyncedAtAsync(
                "teams",
                league.ApiLeagueId,
                season,
                syncedAtUtc,
                cancellationToken);

            totalProcessed += result.Processed;
            totalInserted += result.Inserted;
            totalUpdated += result.Updated;
        }

        return Ok(new
        {
            Message = "Teams synced for multiple leagues.",
            Season = season,
            LeaguesProcessed = leagues.Count,
            LastSyncedAtUtc = syncedAtUtc,
            TotalProcessed = totalProcessed,
            TotalInserted = totalInserted,
            TotalUpdated = totalUpdated
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var teams = await _dbContext.Teams
            .AsNoTracking()
            .Include(x => x.Country)
            .OrderBy(x => x.Name)
            .Select(x => new TeamDto
            {
                Id = x.Id,
                ApiTeamId = x.ApiTeamId,
                Name = x.Name,
                Code = x.Code,
                LogoUrl = x.LogoUrl,
                Founded = x.Founded,
                IsNational = x.IsNational,
                VenueName = x.VenueName,
                VenueAddress = x.VenueAddress,
                VenueCity = x.VenueCity,
                VenueCapacity = x.VenueCapacity,
                VenueSurface = x.VenueSurface,
                VenueImageUrl = x.VenueImageUrl,
                CountryId = x.CountryId,
                CountryName = x.Country != null ? x.Country.Name : null
            })
            .ToListAsync(cancellationToken);

        return Ok(teams);
    }

    [HttpPost("statistics/sync")]
    public async Task<IActionResult> SyncStatistics(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        [FromQuery] long? teamId,
        [FromQuery] int maxTeams = 25,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        if (maxTeams <= 0)
            return BadRequest("maxTeams must be greater than 0.");

        var result = await _teamAnalyticsService.SyncStatisticsAsync(
            leagueId,
            season,
            teamId,
            maxTeams,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{apiTeamId:long}/statistics")]
    public async Task<IActionResult> GetStatistics(
        long apiTeamId,
        [FromQuery] long leagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var statistics = await _teamAnalyticsService.GetTeamStatisticsAsync(
            apiTeamId,
            leagueId,
            season,
            cancellationToken);

        return statistics is null
            ? NotFound($"No stored team statistics were found for team {apiTeamId}, league {leagueId}, season {season}.")
            : Ok(statistics);
    }

    [HttpGet("{apiTeamId:long}/form")]
    public async Task<IActionResult> GetForm(
        long apiTeamId,
        [FromQuery] long leagueId,
        [FromQuery] int season,
        [FromQuery] int last = 5,
        CancellationToken cancellationToken = default)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var form = await _teamAnalyticsService.GetTeamFormAsync(
            apiTeamId,
            leagueId,
            season,
            last,
            cancellationToken);

        return form is null
            ? NotFound($"Team {apiTeamId} or league {leagueId} season {season} was not found.")
            : Ok(form);
    }
}
