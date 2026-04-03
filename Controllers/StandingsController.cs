using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandingsController : ControllerBase
{
    private readonly StandingsSyncService _syncService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public StandingsController(
        StandingsSyncService syncService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _syncService = syncService;
        _dbContext = dbContext;
        _syncStateService = syncStateService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        CancellationToken cancellationToken)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var result = await _syncService.SyncStandingsAsync(leagueId, season, cancellationToken);
        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "standings",
            leagueId,
            season,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Standings synced successfully.",
            LeagueId = leagueId,
            Season = season,
            LastSyncedAtUtc = syncedAtUtc,
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
        CancellationToken cancellationToken)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var standings = await _dbContext.Standings
            .AsNoTracking()
            .Include(x => x.League)
            .Include(x => x.Team)
            .Where(x => x.League.ApiLeagueId == leagueId && x.Season == season)
            .OrderBy(x => x.Rank)
            .Select(x => new StandingDto
            {
                Rank = x.Rank,
                TeamId = x.TeamId,
                ApiTeamId = x.Team.ApiTeamId,
                TeamName = x.Team.Name,
                TeamLogoUrl = x.Team.LogoUrl,
                Points = x.Points,
                GoalsDiff = x.GoalsDiff,
                GroupName = x.GroupName,
                Form = x.Form,
                Status = x.Status,
                Description = x.Description,
                Played = x.Played,
                Win = x.Win,
                Draw = x.Draw,
                Lose = x.Lose,
                GoalsFor = x.GoalsFor,
                GoalsAgainst = x.GoalsAgainst
            })
            .ToListAsync(cancellationToken);

        return Ok(standings);
    }
}
