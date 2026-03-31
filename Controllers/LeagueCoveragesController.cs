using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/league-coverages")]
public class LeagueCoveragesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public LeagueCoveragesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? season,
        [FromQuery] long? leagueId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.LeagueSeasonCoverages
            .AsNoTracking()
            .AsQueryable();

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        if (leagueId.HasValue)
        {
            query = query.Where(x => x.LeagueApiId == leagueId.Value);
        }

        var items = await query
            .OrderBy(x => x.Season)
            .ThenBy(x => x.LeagueApiId)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(x => new LeagueSeasonCoverageDto
        {
            LeagueApiId = x.LeagueApiId,
            Season = x.Season,
            CreatedAtUtc = x.CreatedAt,
            UpdatedAtUtc = x.UpdatedAt,
            Coverage = new LeagueCoverageFlagsDto
            {
                HasFixtures = x.HasFixtures,
                HasFixtureEvents = x.HasFixtureEvents,
                HasLineups = x.HasLineups,
                HasFixtureStatistics = x.HasFixtureStatistics,
                HasPlayerStatistics = x.HasPlayerStatistics,
                HasStandings = x.HasStandings,
                HasPlayers = x.HasPlayers,
                HasTopScorers = x.HasTopScorers,
                HasTopAssists = x.HasTopAssists,
                HasTopCards = x.HasTopCards,
                HasInjuries = x.HasInjuries,
                HasPredictions = x.HasPredictions,
                HasOdds = x.HasOdds
            }
        }).ToList());
    }
}
