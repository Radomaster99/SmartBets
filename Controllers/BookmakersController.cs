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

    [HttpPost("sync-reference")]
    public async Task<IActionResult> SyncReference(CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncReferenceBookmakersAsync(cancellationToken);
        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "bookmakers_reference",
            null,
            null,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Bookmakers refreshed from API-Football reference endpoint successfully.",
            LastSyncedAtUtc = syncedAtUtc,
            result.Source,
            result.RemoteCallsMade,
            result.Processed,
            result.Inserted,
            result.Updated
        });
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
            Message = "Bookmakers refreshed from local odds cache successfully.",
            LeagueId = leagueId,
            Season = season,
            LastSyncedAtUtc = syncedAtUtc,
            result.Source,
            result.RemoteCallsMade,
            result.PreMatchOddsReferences,
            result.LiveOddsReferences,
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
