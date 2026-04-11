using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Enums;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/odds/live/the-odds")]
public class AdminLiveOddsController : ControllerBase
{
    private static readonly HashSet<string> LiveStatuses = FixtureStatusMapper
        .GetStatusesForBucket(FixtureStateBucket.Live)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _dbContext;
    private readonly TheOddsLiveOddsService _theOddsLiveOddsService;
    private readonly TheOddsViewerActivityService _theOddsViewerActivityService;
    private readonly TheOddsViewerRefreshStateService _theOddsViewerRefreshStateService;

    public AdminLiveOddsController(
        AppDbContext dbContext,
        TheOddsLiveOddsService theOddsLiveOddsService,
        TheOddsViewerActivityService theOddsViewerActivityService,
        TheOddsViewerRefreshStateService theOddsViewerRefreshStateService)
    {
        _dbContext = dbContext;
        _theOddsLiveOddsService = theOddsLiveOddsService;
        _theOddsViewerActivityService = theOddsViewerActivityService;
        _theOddsViewerRefreshStateService = theOddsViewerRefreshStateService;
    }

    [HttpGet("viewer-refresh")]
    public async Task<IActionResult> GetViewerRefreshState(CancellationToken cancellationToken = default)
    {
        var state = await _theOddsViewerRefreshStateService.GetStateAsync(cancellationToken);
        return Ok(MapViewerRefreshState(state));
    }

    [HttpPatch("viewer-refresh")]
    public async Task<IActionResult> UpdateViewerRefreshState(
        [FromBody] AdminTheOddsViewerRefreshUpdateRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest("Request body is required.");

        var updatedBy = User.FindFirstValue(ClaimTypes.Name) ??
                        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                        User.FindFirstValue("sub") ??
                        User.FindFirstValue("unique_name") ??
                        "admin";

        var state = await _theOddsViewerRefreshStateService.SetLiveOddsHeartbeatEnabledAsync(
            request.LiveOddsHeartbeatEnabled,
            updatedBy,
            cancellationToken);

        if (!request.LiveOddsHeartbeatEnabled)
        {
            _theOddsViewerActivityService.ClearActiveFixtures();
        }

        return Ok(MapViewerRefreshState(state));
    }

    [HttpPost("refresh-fixture")]
    public async Task<IActionResult> RefreshFixture(
        [FromQuery] long apiFixtureId,
        [FromQuery] bool force = false,
        [FromQuery] bool latestOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (apiFixtureId <= 0)
            return BadRequest("apiFixtureId must be greater than 0.");

        var fixture = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x => x.ApiFixtureId == apiFixtureId)
            .Select(x => new
            {
                x.ApiFixtureId,
                x.KickoffAt,
                x.Status,
                x.Elapsed,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (fixture is null)
        {
            return NotFound(new
            {
                Message = "Fixture not found."
            });
        }

        var cachedItems = await _theOddsLiveOddsService.GetStoredLiveOddsAsync(
            apiFixtureId: apiFixtureId,
            latestOnly: latestOnly,
            cancellationToken: cancellationToken);

        var servedFromCache = !force && cachedItems.Count > 0;
        var refreshedRemotely = false;
        TheOddsLiveOddsSyncResultDto? sync = null;

        if (!servedFromCache)
        {
            sync = await _theOddsLiveOddsService.SyncFixtureLiveOddsAsync(
                apiFixtureId,
                force: force,
                cancellationToken: cancellationToken);

            refreshedRemotely = true;

            cachedItems = await _theOddsLiveOddsService.GetStoredLiveOddsAsync(
                apiFixtureId: apiFixtureId,
                latestOnly: latestOnly,
                cancellationToken: cancellationToken);
        }

        return Ok(new AdminTheOddsFixtureRefreshResultDto
        {
            ApiFixtureId = fixture.ApiFixtureId,
            KickoffAtUtc = fixture.KickoffAt,
            Status = fixture.Status,
            Elapsed = fixture.Elapsed,
            HomeTeamName = fixture.HomeTeamName,
            AwayTeamName = fixture.AwayTeamName,
            Forced = force,
            ServedFromCache = servedFromCache,
            RefreshedRemotely = refreshedRemotely,
            HasCachedOdds = cachedItems.Count > 0,
            MarketsReturned = cachedItems.Count,
            Sync = sync,
            Items = cachedItems
        });
    }

    [HttpPost("refresh-league")]
    public async Task<IActionResult> RefreshLeague(
        [FromQuery] long leagueId,
        [FromQuery] int season,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (leagueId <= 0)
            return BadRequest("leagueId must be greater than 0.");

        if (season <= 0)
            return BadRequest("season must be greater than 0.");

        var liveFixtures = await _dbContext.Fixtures
            .AsNoTracking()
            .Where(x =>
                x.League.ApiLeagueId == leagueId &&
                x.Season == season &&
                x.Status != null &&
                LiveStatuses.Contains(x.Status))
            .OrderBy(x => x.KickoffAt)
            .ThenBy(x => x.ApiFixtureId)
            .Select(x => new AdminTheOddsLeagueFixtureItemDto
            {
                ApiFixtureId = x.ApiFixtureId,
                KickoffAtUtc = x.KickoffAt,
                Status = x.Status,
                Elapsed = x.Elapsed,
                HomeTeamName = x.HomeTeam.Name,
                AwayTeamName = x.AwayTeam.Name
            })
            .ToListAsync(cancellationToken);

        var cachedSummaries = liveFixtures.Count == 0
            ? Array.Empty<FixtureLiveOddsSummaryDto>()
            : (await _theOddsLiveOddsService.GetFixtureOddsSummariesAsync(
                liveFixtures.Select(x => x.ApiFixtureId).ToList(),
                cancellationToken)).ToArray();

        var cachedSummariesByApiFixtureId = cachedSummaries.ToDictionary(x => x.ApiFixtureId);
        var fixturesMissingCachedOdds = Math.Max(0, liveFixtures.Count - cachedSummaries.Length);
        var servedFromCache = !force && liveFixtures.Count > 0 && fixturesMissingCachedOdds == 0;
        var refreshedRemotely = false;
        TheOddsLiveOddsSyncResultDto? sync = null;

        if (force || (!servedFromCache && liveFixtures.Count > 0))
        {
            sync = await _theOddsLiveOddsService.SyncLeagueLiveOddsAsync(
                leagueId,
                season,
                force: force,
                cancellationToken: cancellationToken);

            refreshedRemotely = true;

            cachedSummaries = liveFixtures.Count == 0
                ? Array.Empty<FixtureLiveOddsSummaryDto>()
                : (await _theOddsLiveOddsService.GetFixtureOddsSummariesAsync(
                    liveFixtures.Select(x => x.ApiFixtureId).ToList(),
                    cancellationToken)).ToArray();

            cachedSummariesByApiFixtureId = cachedSummaries.ToDictionary(x => x.ApiFixtureId);
            fixturesMissingCachedOdds = Math.Max(0, liveFixtures.Count - cachedSummaries.Length);
        }

        foreach (var fixture in liveFixtures)
        {
            if (cachedSummariesByApiFixtureId.TryGetValue(fixture.ApiFixtureId, out var summary))
            {
                fixture.HasCachedOdds = true;
                fixture.Summary = summary;
            }
        }

        return Ok(new AdminTheOddsLeagueRefreshResultDto
        {
            LeagueApiId = leagueId,
            Season = season,
            Forced = force,
            ServedFromCache = servedFromCache,
            RefreshedRemotely = refreshedRemotely,
            LiveFixturesInScope = liveFixtures.Count,
            FixturesWithCachedOdds = cachedSummaries.Length,
            FixturesMissingCachedOdds = fixturesMissingCachedOdds,
            Sync = sync,
            Items = liveFixtures
        });
    }

    private AdminTheOddsViewerRefreshStateDto MapViewerRefreshState(TheOddsViewerRefreshStateSnapshot state)
    {
        return new AdminTheOddsViewerRefreshStateDto
        {
            LiveOddsHeartbeatEnabled = state.LiveOddsHeartbeatEnabled,
            TheOddsProviderEnabled = state.TheOddsProviderEnabled,
            TheOddsProviderConfigured = state.TheOddsProviderConfigured,
            ConfigViewerDrivenRefreshEnabled = state.ConfigViewerDrivenRefreshEnabled,
            EffectiveViewerDrivenRefreshEnabled = state.EffectiveViewerDrivenRefreshEnabled,
            ReadDrivenCatchUpEnabled = state.ReadDrivenCatchUpEnabled,
            ViewerHeartbeatTtlSeconds = state.ViewerHeartbeatTtlSeconds,
            ViewerRefreshIntervalSeconds = state.ViewerRefreshIntervalSeconds,
            ActiveFixtureIds = _theOddsViewerActivityService.GetActiveFixtureCount(),
            UpdatedAtUtc = state.UpdatedAtUtc,
            UpdatedBy = state.UpdatedBy
        };
    }
}
