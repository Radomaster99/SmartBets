using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Enums;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FixturesController : ControllerBase
{
    private readonly FixtureSyncService _fixtureSyncService;
    private readonly AppDbContext _dbContext;

    public FixturesController(FixtureSyncService fixtureSyncService, AppDbContext dbContext)
    {
        _fixtureSyncService = fixtureSyncService ?? throw new ArgumentNullException(nameof(fixtureSyncService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] int season,
        [FromQuery] long? leagueId,
        [FromQuery] int maxLeagues = 5,
        CancellationToken cancellationToken = default)
    {
        if (leagueId.HasValue)
        {
            var result = await _fixtureSyncService.SyncFixturesAsync(
                leagueId.Value,
                season,
                cancellationToken);

            return Ok(new
            {
                Message = "Fixtures synced for specific league.",
                LeagueId = leagueId,
                Season = season,
                result.Processed,
                result.Inserted,
                result.Updated,
                result.SkippedMissingTeams
            });
        }

        var leagues = await _dbContext.Leagues
            .AsNoTracking()
            .Where(x => x.Season == season)
            .Take(maxLeagues)
            .ToListAsync(cancellationToken);

        var totalProcessed = 0;
        var totalInserted = 0;
        var totalUpdated = 0;
        var totalSkippedMissingTeams = 0;

        foreach (var league in leagues)
        {
            var result = await _fixtureSyncService.SyncFixturesAsync(
                league.ApiLeagueId,
                season,
                cancellationToken);

            totalProcessed += result.Processed;
            totalInserted += result.Inserted;
            totalUpdated += result.Updated;
            totalSkippedMissingTeams += result.SkippedMissingTeams;
        }

        return Ok(new
        {
            Message = "Fixtures synced for multiple leagues.",
            Season = season,
            LeaguesProcessed = leagues.Count,
            TotalProcessed = totalProcessed,
            TotalInserted = totalInserted,
            TotalUpdated = totalUpdated,
            TotalSkippedMissingTeams = totalSkippedMissingTeams
        });
    }
    [HttpPost("sync-upcoming")]
    public async Task<IActionResult> SyncUpcoming(
    [FromQuery] long leagueId,
    [FromQuery] int season,
    CancellationToken cancellationToken)
    {
        var result = await _fixtureSyncService.SyncUpcomingFixturesAsync(
            leagueId,
            season,
            cancellationToken);

        return Ok(new
        {
            Message = "Upcoming fixtures synced successfully.",
            LeagueId = leagueId,
            Season = season,
            result.Processed,
            result.Inserted,
            result.Updated,
            result.SkippedMissingTeams
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        [FromQuery] FixtureStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Fixtures
            .AsNoTracking()
            .Include(x => x.League)
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .Where(x => x.League.ApiLeagueId == leagueId && x.Season == season)
            .AsQueryable();

        if (status.HasValue)
        {
            var statusValue = status.Value.ToString();
            query = query.Where(x => x.Status == statusValue);
        }

        var fixtures = await query
            .OrderBy(x => x.KickoffAt)
            .Select(x => new FixtureDto
            {
                Id = x.Id,
                ApiFixtureId = x.ApiFixtureId,
                Season = x.Season,
                KickoffAt = x.KickoffAt,
                Status = x.Status,
                LeagueId = x.League.ApiLeagueId,
                LeagueName = x.League.Name,
                HomeTeamId = x.HomeTeamId,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamId = x.AwayTeamId,
                AwayTeamName = x.AwayTeam.Name,
                HomeGoals = x.HomeGoals,
                AwayGoals = x.AwayGoals
            })
            .ToListAsync(cancellationToken);

        return Ok(fixtures);
    }

}