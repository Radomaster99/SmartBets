using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/sync-errors")]
public class SyncErrorsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public SyncErrorsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType,
        [FromQuery] long? leagueId,
        [FromQuery] int? season,
        [FromQuery] string? source,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = _dbContext.SyncErrors
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim();
            query = query.Where(x => x.EntityType == normalizedEntityType);
        }

        if (leagueId.HasValue)
        {
            query = query.Where(x => x.LeagueApiId == leagueId.Value);
        }

        if (season.HasValue)
        {
            query = query.Where(x => x.Season == season.Value);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var normalizedSource = source.Trim();
            query = query.Where(x => x.Source == normalizedSource);
        }

        var errors = await query
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .Select(x => new SyncErrorDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                Operation = x.Operation,
                LeagueApiId = x.LeagueApiId,
                Season = x.Season,
                Source = x.Source,
                ErrorMessage = x.ErrorMessage,
                OccurredAtUtc = x.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return Ok(errors);
    }
}
