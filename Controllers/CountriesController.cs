using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    private readonly CountrySyncService _countrySyncService;
    private readonly AppDbContext _dbContext;
    private readonly SyncStateService _syncStateService;

    public CountriesController(
        CountrySyncService countrySyncService,
        AppDbContext dbContext,
        SyncStateService syncStateService)
    {
        _countrySyncService = countrySyncService;
        _dbContext = dbContext;
        _syncStateService = syncStateService;
    }

    [Authorize(Roles = "admin")]
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var result = await _countrySyncService.SyncCountriesAsync(cancellationToken);
        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "countries",
            null,
            null,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Countries synced successfully.",
            LastSyncedAtUtc = syncedAtUtc,
            result.Processed,
            result.Inserted,
            result.Updated
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var countries = await _dbContext.Countries
            .OrderBy(x => x.Name)
            .Select(x => new CountryDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                FlagUrl = x.FlagUrl
            })
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("debug-count")]
    public async Task<IActionResult> DebugCount(CancellationToken cancellationToken)
    {
        var count = await _dbContext.Countries.CountAsync(cancellationToken);

        return Ok(new { count });
    }
}
