using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookmakersController : ControllerBase
{
    private readonly BookmakerSyncService _syncService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public BookmakersController(
        BookmakerSyncService syncService,
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
        var result = await _syncService.SyncBookmakersAsync(leagueId, season, cancellationToken);
        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "bookmakers",
            leagueId,
            season,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Bookmakers synced successfully.",
            LeagueId = leagueId,
            Season = season,
            LastSyncedAtUtc = syncedAtUtc,
            result.Processed,
            result.Inserted,
            result.Updated
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var bookmakers = await _dbContext.Bookmakers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BookmakerDto
            {
                Id = x.Id,
                ApiBookmakerId = x.ApiBookmakerId,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(bookmakers);
    }
}
