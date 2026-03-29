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

    public BookmakersController(BookmakerSyncService syncService, AppDbContext dbContext)
    {
        _syncService = syncService;
        _dbContext = dbContext;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromQuery] long leagueId, [FromQuery] int season, CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncBookmakersAsync(leagueId, season, cancellationToken);

        return Ok(new
        {
            Message = "Bookmakers synced successfully.",
            LeagueId = leagueId,
            Season = season,
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