using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;
using SmartBets.Enums;
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
    private readonly AppDbContext _dbContext;

    public OddsController(
        AppDbContext dbContext,
        PreMatchOddsService preMatchOddsService,
        LiveOddsService liveOddsService,
        OddsAnalyticsService oddsAnalyticsService,
        SyncStateService syncStateService)
    {
        _dbContext = dbContext;
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

        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        var odds = await GetCurrentOddsAsync(
            fixture,
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

        var fixture = await ResolveFixtureAsync(fixtureId, apiFixtureId, cancellationToken);
        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        var bestOdds = await GetCurrentBestOddsAsync(
            fixture,
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

    private async Task<Fixture?> ResolveFixtureAsync(
        long? fixtureId,
        long? apiFixtureId,
        CancellationToken cancellationToken)
    {
        if (!fixtureId.HasValue && !apiFixtureId.HasValue)
            return null;

        var query = _dbContext.Fixtures
            .AsNoTracking()
            .AsQueryable();

        if (fixtureId.HasValue)
            query = query.Where(x => x.Id == fixtureId.Value);

        if (apiFixtureId.HasValue)
            query = query.Where(x => x.ApiFixtureId == apiFixtureId.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Dtos.OddDto>> GetCurrentOddsAsync(
        Fixture fixture,
        string? marketName,
        bool latestOnly,
        CancellationToken cancellationToken)
    {
        if (ShouldPreferLiveOdds(fixture, marketName, latestOnly))
        {
            var liveOdds = await _liveOddsService.GetMatchWinnerOddsAsync(
                fixtureId: fixture.Id,
                latestOnly: true,
                cancellationToken: cancellationToken);

            if (liveOdds.Count > 0)
                return liveOdds;
        }

        return await _preMatchOddsService.GetFixtureOddsAsync(
            fixtureId: fixture.Id,
            marketName: marketName,
            latestOnly: latestOnly,
            cancellationToken: cancellationToken);
    }

    private async Task<Dtos.BestOddsDto?> GetCurrentBestOddsAsync(
        Fixture fixture,
        string? marketName,
        CancellationToken cancellationToken)
    {
        if (ShouldPreferLiveOdds(fixture, marketName, latestOnly: true))
        {
            var liveBestOdds = await _liveOddsService.GetBestMatchWinnerOddsAsync(
                fixtureId: fixture.Id,
                cancellationToken: cancellationToken);

            if (liveBestOdds is not null)
                return liveBestOdds;
        }

        return await _preMatchOddsService.GetBestOddsAsync(
            fixtureId: fixture.Id,
            marketName: marketName,
            cancellationToken: cancellationToken);
    }

    private static bool ShouldPreferLiveOdds(Fixture fixture, string? marketName, bool latestOnly)
    {
        if (!latestOnly)
            return false;

        if (!string.IsNullOrWhiteSpace(marketName) &&
            !string.Equals(marketName.Trim(), PreMatchOddsService.DefaultMarketName, StringComparison.OrdinalIgnoreCase))
            return false;

        return FixtureStatusMapper.GetStateBucket(fixture.Status) == FixtureStateBucket.Live;
    }
}
