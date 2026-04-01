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

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkUpsert(
        [FromBody] SupportedLeaguesBulkUpsertRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Items is null || request.Items.Count == 0)
        {
            return BadRequest("items must contain at least one supported league.");
        }

        var result = new SupportedLeaguesBulkUpsertResultDto
        {
            Received = request.Items.Count
        };

        var payloadKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            var payloadKey = BuildLeagueSeasonKey(item.LeagueApiId, item.Season);
            if (!payloadKeys.Add(payloadKey))
            {
                result.Failed++;
                result.Results.Add(new SupportedLeaguesBulkUpsertItemResultDto
                {
                    LeagueApiId = item.LeagueApiId,
                    Season = item.Season,
                    IsActive = item.IsActive,
                    Priority = item.Priority,
                    Status = "Failed",
                    Error = "Duplicate leagueApiId + season found in payload."
                });
            }
        }

        var validItems = request.Items
            .Where(x => payloadKeys.Contains(BuildLeagueSeasonKey(x.LeagueApiId, x.Season)))
            .DistinctBy(x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season))
            .ToList();

        var leagueApiIds = validItems
            .Where(x => x.LeagueApiId > 0)
            .Select(x => x.LeagueApiId)
            .Distinct()
            .ToArray();

        var seasons = validItems
            .Where(x => x.Season > 0)
            .Select(x => x.Season)
            .Distinct()
            .ToArray();

        var localLeagues = await _dbContext.Leagues
            .AsNoTracking()
            .Where(x => leagueApiIds.Contains(x.ApiLeagueId) && seasons.Contains(x.Season))
            .Select(x => new { x.ApiLeagueId, x.Season })
            .ToListAsync(cancellationToken);

        var localLeagueKeys = localLeagues
            .Select(x => BuildLeagueSeasonKey(x.ApiLeagueId, x.Season))
            .ToHashSet(StringComparer.Ordinal);

        var existingSupportedLeagues = await _dbContext.SupportedLeagues
            .Where(x => leagueApiIds.Contains(x.LeagueApiId) && seasons.Contains(x.Season))
            .ToListAsync(cancellationToken);

        var existingLookup = existingSupportedLeagues.ToDictionary(
            x => BuildLeagueSeasonKey(x.LeagueApiId, x.Season),
            x => x,
            StringComparer.Ordinal);

        foreach (var item in validItems)
        {
            var itemResult = new SupportedLeaguesBulkUpsertItemResultDto
            {
                LeagueApiId = item.LeagueApiId,
                Season = item.Season,
                IsActive = item.IsActive,
                Priority = item.Priority
            };

            if (item.LeagueApiId <= 0)
            {
                itemResult.Status = "Failed";
                itemResult.Error = "leagueApiId must be greater than 0.";
                result.Failed++;
                result.Results.Add(itemResult);
                continue;
            }

            if (item.Season <= 0)
            {
                itemResult.Status = "Failed";
                itemResult.Error = "season must be greater than 0.";
                result.Failed++;
                result.Results.Add(itemResult);
                continue;
            }

            if (item.Priority < 0)
            {
                itemResult.Status = "Failed";
                itemResult.Error = "priority cannot be negative.";
                result.Failed++;
                result.Results.Add(itemResult);
                continue;
            }

            var key = BuildLeagueSeasonKey(item.LeagueApiId, item.Season);

            if (!localLeagueKeys.Contains(key))
            {
                itemResult.Status = "Failed";
                itemResult.Error = "League/season is missing from local leagues table. Run countries sync and leagues sync first.";
                result.Failed++;
                result.Results.Add(itemResult);
                continue;
            }

            if (existingLookup.TryGetValue(key, out var existing))
            {
                var changed = false;

                if (existing.IsActive != item.IsActive)
                {
                    existing.IsActive = item.IsActive;
                    changed = true;
                }

                if (existing.Priority != item.Priority)
                {
                    existing.Priority = item.Priority;
                    changed = true;
                }

                itemResult.Id = existing.Id;
                itemResult.Status = changed ? "Updated" : "Unchanged";

                if (changed)
                {
                    result.Updated++;
                }
                else
                {
                    result.Unchanged++;
                }
            }
            else
            {
                var entity = new SupportedLeague
                {
                    LeagueApiId = item.LeagueApiId,
                    Season = item.Season,
                    IsActive = item.IsActive,
                    Priority = item.Priority,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.SupportedLeagues.Add(entity);
                existingLookup[key] = entity;

                itemResult.Status = "Created";
                result.Created++;
            }

            result.Results.Add(itemResult);
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var itemResult in result.Results.Where(x => x.Id is null && x.Status is "Created"))
            {
                var key = BuildLeagueSeasonKey(itemResult.LeagueApiId, itemResult.Season);
                if (existingLookup.TryGetValue(key, out var entity))
                {
                    itemResult.Id = entity.Id;
                }
            }
        }

        return Ok(result);
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

    private static string BuildLeagueSeasonKey(long leagueApiId, int season)
    {
        return $"{leagueApiId}:{season}";
    }
}
