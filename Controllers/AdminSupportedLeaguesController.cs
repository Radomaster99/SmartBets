using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Entities;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/admin/supported-leagues")]
public class AdminSupportedLeaguesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AdminSupportedLeaguesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.SupportedLeagues
            .AsNoTracking()
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.LeagueApiId)
            .Select(x => new
            {
                x.Id,
                x.LeagueApiId,
                x.Season,
                x.IsActive,
                x.Priority,
                CreatedAtUtc = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.SupportedLeagues
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.LeagueApiId,
                x.Season,
                x.IsActive,
                x.Priority,
                CreatedAtUtc = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return item is null
            ? NotFound(new { Message = "Supported league entry not found." })
            : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SupportedLeagueUpsertDto? request,
        [FromQuery] long? leagueApiId,
        [FromQuery] int? season,
        [FromQuery] bool? isActive,
        [FromQuery] int? priority,
        CancellationToken cancellationToken = default)
    {
        request ??= new SupportedLeagueUpsertDto();

        if (leagueApiId.HasValue)
            request.LeagueApiId = leagueApiId.Value;

        if (season.HasValue)
            request.Season = season.Value;

        if (isActive.HasValue)
            request.IsActive = isActive.Value;

        if (priority.HasValue)
            request.Priority = priority.Value;

        if (request.LeagueApiId <= 0)
            return BadRequest("leagueApiId must be greater than 0.");

        if (request.Season <= 0)
            return BadRequest("season must be greater than 0.");

        if (request.Priority < 0)
            return BadRequest("priority cannot be negative.");

        var leagueExists = await _dbContext.Leagues
            .AsNoTracking()
            .AnyAsync(
                x => x.ApiLeagueId == request.LeagueApiId && x.Season == request.Season,
                cancellationToken);

        if (!leagueExists)
        {
            return BadRequest("The requested league and season do not exist in the local leagues table.");
        }

        var exists = await _dbContext.SupportedLeagues
            .AsNoTracking()
            .AnyAsync(
                x => x.LeagueApiId == request.LeagueApiId && x.Season == request.Season,
                cancellationToken);

        if (exists)
        {
            return Conflict("A supported league entry for this league and season already exists.");
        }

        var entity = new SupportedLeague
        {
            LeagueApiId = request.LeagueApiId,
            Season = request.Season,
            IsActive = request.IsActive,
            Priority = request.Priority,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.SupportedLeagues.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/admin/supported-leagues/{entity.Id}", new
        {
            entity.Id,
            entity.LeagueApiId,
            entity.Season,
            entity.IsActive,
            entity.Priority,
            CreatedAtUtc = entity.CreatedAt
        });
    }

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] SupportedLeagueUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SupportedLeagues
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new
            {
                Message = "Supported league entry not found."
            });
        }

        if (request.Priority.HasValue && request.Priority.Value < 0)
            return BadRequest("priority cannot be negative.");

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        if (request.Priority.HasValue)
        {
            entity.Priority = request.Priority.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            entity.Id,
            entity.LeagueApiId,
            entity.Season,
            entity.IsActive,
            entity.Priority,
            CreatedAtUtc = entity.CreatedAt
        });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SupportedLeagues
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new
            {
                Message = "Supported league entry not found."
            });
        }

        _dbContext.SupportedLeagues.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
