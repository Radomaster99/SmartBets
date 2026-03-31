using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OddsController : ControllerBase
{
    private readonly PreMatchOddsService _preMatchOddsService;
    private readonly SyncStateService _syncStateService;

    public OddsController(
        PreMatchOddsService preMatchOddsService,
        SyncStateService syncStateService)
    {
        _preMatchOddsService = preMatchOddsService;
        _syncStateService = syncStateService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken)
    {
        var result = await _preMatchOddsService.SyncOddsAsync(
            leagueId,
            season,
            marketName,
            cancellationToken);

        var syncedAtUtc = DateTime.UtcNow;

        await _syncStateService.SetLastSyncedAtAsync(
            "odds",
            leagueId,
            season,
            syncedAtUtc,
            cancellationToken);

        await _syncStateService.SetLastSyncedAtAsync(
            "bookmakers",
            leagueId,
            season,
            syncedAtUtc,
            cancellationToken);

        return Ok(new
        {
            Message = "Pre-match odds synced successfully.",
            LeagueId = leagueId,
            Season = season,
            LastSyncedAtUtc = syncedAtUtc,
            result.MarketName,
            result.FixturesMatched,
            result.FixturesMissingInDatabase,
            result.BookmakersProcessed,
            result.BookmakersInserted,
            result.BookmakersUpdated,
            result.SnapshotsProcessed,
            result.SnapshotsInserted,
            result.SnapshotsSkippedUnchanged,
            result.SnapshotsSkippedUnsupportedMarket
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetOdds(
        [FromQuery] long? fixtureId,
        [FromQuery] long? apiFixtureId,
        [FromQuery] string? marketName,
        [FromQuery] bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return BadRequest("Either fixtureId or apiFixtureId is required.");

        var odds = await _preMatchOddsService.GetFixtureOddsAsync(
            fixtureId,
            apiFixtureId,
            marketName,
            latestOnly,
            cancellationToken);

        if (odds.Count == 0)
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
        [FromQuery] long? fixtureId,
        [FromQuery] long? apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return BadRequest("Either fixtureId or apiFixtureId is required.");

        var bestOdds = await _preMatchOddsService.GetBestOddsAsync(
            fixtureId,
            apiFixtureId,
            marketName,
            cancellationToken);

        if (bestOdds is null)
        {
            return NotFound(new
            {
                Message = "No odds found for this fixture."
            });
        }

        return Ok(bestOdds);
    }
}
