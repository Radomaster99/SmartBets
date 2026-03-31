using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaguesController : ControllerBase
{
    private readonly LeagueSyncService _syncService;
    private readonly LeagueAnalyticsService _leagueAnalyticsService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public LeaguesController(
        LeagueSyncService syncService,
        LeagueAnalyticsService leagueAnalyticsService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _syncService = syncService;
        _leagueAnalyticsService = leagueAnalyticsService;
        _dbContext = dbContext;
        _syncStateService = syncStateService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var processed = await _syncService.SyncLeaguesAsync(cancellationToken);
        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "leagues",
            null,
            null,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Leagues synced successfully.",
            LastSyncedAtUtc = syncedAtUtc,
            Processed = processed
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? season, CancellationToken cancellationToken)
    {
        var query = _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .AsQueryable();

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        var leagues = await query
            .OrderBy(x => x.Country.Name)
            .ThenBy(x => x.Name)
            .ThenByDescending(x => x.Season)
            .Select(x => new LeagueDto
            {
                Id = x.Id,
                ApiLeagueId = x.ApiLeagueId,
                Name = x.Name,
                Season = x.Season,
                CountryId = x.CountryId,
                CountryName = x.Country.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(leagues);
    }

    [HttpPost("{apiLeagueId:long}/analytics/sync")]
    public async Task<IActionResult> SyncAnalytics(
        long apiLeagueId,
        [FromQuery] int season,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var result = await _leagueAnalyticsService.SyncLeagueAnalyticsAsync(
            apiLeagueId,
            season,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("analytics/sync")]
    public async Task<IActionResult> SyncSupportedLeagueAnalytics(
        [FromQuery] int? season,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxLeagues = 10,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (maxLeagues <= 0)
            return BadRequest("maxLeagues must be greater than 0.");

        var result = await _leagueAnalyticsService.SyncSupportedLeaguesAsync(
            season,
            activeOnly,
            maxLeagues,
            force,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{apiLeagueId:long}/rounds")]
    public async Task<IActionResult> GetRounds(
        long apiLeagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
            return NotFound($"League {apiLeagueId} season {season} was not found.");

        var rounds = await _leagueAnalyticsService.GetRoundsAsync(apiLeagueId, season, cancellationToken);
        return Ok(rounds);
    }

    [HttpGet("{apiLeagueId:long}/current-round")]
    public async Task<IActionResult> GetCurrentRound(
        long apiLeagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
            return NotFound($"League {apiLeagueId} season {season} was not found.");

        var round = await _leagueAnalyticsService.GetCurrentRoundAsync(apiLeagueId, season, cancellationToken);
        return round is null
            ? NotFound($"No current round is stored for league {apiLeagueId}, season {season}.")
            : Ok(round);
    }

    [HttpGet("{apiLeagueId:long}/top-scorers")]
    public async Task<IActionResult> GetTopScorers(
        long apiLeagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
            return NotFound($"League {apiLeagueId} season {season} was not found.");

        var scorers = await _leagueAnalyticsService.GetTopScorersAsync(apiLeagueId, season, cancellationToken);
        return Ok(scorers);
    }

    [HttpGet("{apiLeagueId:long}/top-assists")]
    public async Task<IActionResult> GetTopAssists(
        long apiLeagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
            return NotFound($"League {apiLeagueId} season {season} was not found.");

        var assists = await _leagueAnalyticsService.GetTopAssistsAsync(apiLeagueId, season, cancellationToken);
        return Ok(assists);
    }

    [HttpGet("{apiLeagueId:long}/top-cards")]
    public async Task<IActionResult> GetTopCards(
        long apiLeagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(x => x.ApiLeagueId == apiLeagueId && x.Season == season, cancellationToken);

        if (!leagueExists)
            return NotFound($"League {apiLeagueId} season {season} was not found.");

        var cards = await _leagueAnalyticsService.GetTopCardsAsync(apiLeagueId, season, cancellationToken);
        return Ok(cards);
    }

    [HttpGet("{apiLeagueId:long}/dashboard")]
    public async Task<IActionResult> GetDashboard(
        long apiLeagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken = default)
    {
        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var dashboard = await _leagueAnalyticsService.GetDashboardAsync(apiLeagueId, season, cancellationToken);
        return dashboard is null
            ? NotFound($"League {apiLeagueId} season {season} was not found.")
            : Ok(dashboard);
    }
}
