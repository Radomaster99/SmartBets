using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OddsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public OddsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetOdds(
        [FromQuery] long fixtureId,
        CancellationToken cancellationToken)
    {
        var odds = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Include(x => x.Bookmaker)
            .Where(x => x.FixtureId == fixtureId)
            .Select(x => new OddDto
            {
                Bookmaker = x.Bookmaker.Name,
                HomeOdd = x.HomeOdd,
                DrawOdd = x.DrawOdd,
                AwayOdd = x.AwayOdd
            })
            .ToListAsync(cancellationToken);

        if (!odds.Any())
        {
            return NotFound(new
            {
                Message = "No odds found for this fixture."
            });
        }

        return Ok(odds);
    }

    [HttpGet("best")]
    public async Task<IActionResult> GetBestOdds(
        [FromQuery] long fixtureId,
        CancellationToken cancellationToken)
    {
        var odds = await _dbContext.PreMatchOdds
            .AsNoTracking()
            .Include(x => x.Bookmaker)
            .Where(x => x.FixtureId == fixtureId)
            .ToListAsync(cancellationToken);

        if (!odds.Any())
        {
            return NotFound(new
            {
                Message = "No odds found for this fixture."
            });
        }

        var bestHome = odds
            .Where(x => x.HomeOdd.HasValue)
            .OrderByDescending(x => x.HomeOdd)
            .FirstOrDefault();

        var bestDraw = odds
            .Where(x => x.DrawOdd.HasValue)
            .OrderByDescending(x => x.DrawOdd)
            .FirstOrDefault();

        var bestAway = odds
            .Where(x => x.AwayOdd.HasValue)
            .OrderByDescending(x => x.AwayOdd)
            .FirstOrDefault();

        var result = new BestOddsDto
        {
            FixtureId = fixtureId,

            BestHomeOdd = bestHome?.HomeOdd,
            BestHomeBookmaker = bestHome?.Bookmaker?.Name,

            BestDrawOdd = bestDraw?.DrawOdd,
            BestDrawBookmaker = bestDraw?.Bookmaker?.Name,

            BestAwayOdd = bestAway?.AwayOdd,
            BestAwayBookmaker = bestAway?.Bookmaker?.Name
        };

        return Ok(result);
    }
}