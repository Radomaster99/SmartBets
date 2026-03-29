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
    private readonly AppDbContext _dbContext;

    public LeaguesController(LeagueSyncService syncService, AppDbContext dbContext)
    {
        _syncService = syncService;
        _dbContext = dbContext;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var processed = await _syncService.SyncLeaguesAsync(cancellationToken);

        return Ok(new
        {
            Message = "Leagues synced successfully.",
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
}