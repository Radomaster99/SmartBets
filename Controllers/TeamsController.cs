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
    private readonly AppDbContext _dbContext;

    public TeamsController(TeamSyncService teamSyncService, AppDbContext dbContext)
    {
        _teamSyncService = teamSyncService;
        _dbContext = dbContext;
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
            var result = await _teamSyncService.SyncTeamsAsync(
                leagueId.Value,
                season,
                cancellationToken);

            return Ok(new
            {
                Message = "Teams synced for specific league.",
                LeagueId = leagueId,
                Season = season,
                result.Processed,
                result.Inserted,
                result.Updated
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

        foreach (var league in leagues)
        {
            var result = await _teamSyncService.SyncTeamsAsync(
                league.ApiLeagueId,
                season,
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
                CountryId = x.CountryId,
                CountryName = x.Country != null ? x.Country.Name : null
            })
            .ToListAsync(cancellationToken);

        return Ok(teams);
    }
}