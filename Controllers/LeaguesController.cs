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
    private readonly SyncStateService _syncStateService;

    public LeaguesController(
        LeagueSyncService syncService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _syncService = syncService;
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
}
