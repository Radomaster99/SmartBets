using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OddsController : ControllerBase
{
    private readonly PreMatchOddsService _preMatchOddsService;
    private readonly LiveOddsService _liveOddsService;
    private readonly OddsAnalyticsService _oddsAnalyticsService;
    private readonly SyncStateService _syncStateService;

    public OddsController(
        PreMatchOddsService preMatchOddsService,
        LiveOddsService liveOddsService,
        OddsAnalyticsService oddsAnalyticsService,
        SyncStateService syncStateService)
    {
        _preMatchOddsService = preMatchOddsService;
        _liveOddsService = liveOddsService;
        _oddsAnalyticsService = oddsAnalyticsService;
        _syncStateService = syncStateService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

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

    [HttpPost("live-bets/sync")]
    public async Task<IActionResult> SyncLiveBetTypes(CancellationToken cancellationToken = default)
    {
        var result = await _liveOddsService.SyncLiveBetTypesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("live-bets")]
    public async Task<IActionResult> GetLiveBetTypes(CancellationToken cancellationToken = default)
    {
        var result = await _liveOddsService.GetLiveBetTypesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("live/sync")]
    public async Task<IActionResult> SyncLiveOdds(
        [FromQuery] long? fixtureId,
        [FromQuery] long? leagueId,
        [FromQuery] long? betId,
        [FromQuery] long? bookmakerId,
        CancellationToken cancellationToken = default)
    {
        if (!fixtureId.HasValue && !leagueId.HasValue)
            return BadRequest("Provide fixtureId or leagueId.");

        if (fixtureId.HasValue && fixtureId.Value <= 0)
            return BadRequest("fixtureId must be greater than 0.");

        if (leagueId.HasValue && leagueId.Value <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (betId.HasValue && betId.Value <= 0)
            return BadRequest("betId must be greater than 0.");

        if (bookmakerId.HasValue && bookmakerId.Value <= 0)
            return BadRequest("bookmakerId must be greater than 0.");

        var result = await _liveOddsService.SyncLiveOddsAsync(
            fixtureId,
            leagueId,
            betId,
            bookmakerId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("live")]
    public async Task<IActionResult> GetLiveOdds(
        [FromQuery] long? fixtureId,
        [FromQuery] long? apiFixtureId,
        [FromQuery] long? betId,
        [FromQuery] long? bookmakerId,
        [FromQuery] bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return BadRequest("Either fixtureId or apiFixtureId is required.");

        var result = await _liveOddsService.GetLiveOddsAsync(
            fixtureId,
            apiFixtureId,
            betId,
            bookmakerId,
            latestOnly,
            cancellationToken);

        if (result.Count == 0)
        {
            return NotFound(new
            {
                Message = "No live odds found for this fixture."
            });
        }

        return Ok(result);
    }

    [HttpPost("analytics/rebuild")]
    public async Task<IActionResult> RebuildAnalytics(
        [FromQuery] long? leagueId,
        [FromQuery] int? season,
        [FromQuery] long? apiFixtureId,
        [FromQuery] string? marketName,
        CancellationToken cancellationToken = default)
    {
        if (!apiFixtureId.HasValue && (!leagueId.HasValue || !season.HasValue))
        {
            return BadRequest("Provide apiFixtureId or leagueId + season.");
        }

        if (leagueId.HasValue && leagueId.Value <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season.HasValue && season.Value <= 0)
            return BadRequest("season must be greater than 0.");

        if (apiFixtureId.HasValue && apiFixtureId.Value <= 0)
            return BadRequest("apiFixtureId must be greater than 0.");

        var result = await _oddsAnalyticsService.RebuildAnalyticsAsync(
            leagueId,
            season,
            apiFixtureId,
            marketName,
            cancellationToken);

        if (leagueId.HasValue && season.HasValue)
        {
            await _syncStateService.SetLastSyncedAtAsync(
                "odds_analytics",
                leagueId.Value,
                season.Value,
                result.ExecutedAtUtc,
                cancellationToken);
        }

        return Ok(result);
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
