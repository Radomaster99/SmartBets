using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
[Route("api/[controller]")]
public class PreloadController : ControllerBase
{
    private readonly PreloadSyncService _preloadSyncService;
    private readonly HistoricalBootstrapService _historicalBootstrapService;

    public PreloadController(
        PreloadSyncService preloadSyncService,
        HistoricalBootstrapService historicalBootstrapService)
    {
        _preloadSyncService = preloadSyncService;
        _historicalBootstrapService = historicalBootstrapService;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(
        [FromQuery] int? season,
        [FromQuery] int? maxLeagues,
        [FromQuery] bool force = false,
        [FromQuery] bool stopOnRateLimit = true,
        [FromQuery] int minMinutesSinceLastSync = 180,
        [FromQuery] bool includeOdds = false,
        CancellationToken cancellationToken = default)
    {
        if (maxLeagues.HasValue && maxLeagues.Value <= 0)
            return BadRequest("maxLeagues must be greater than 0.");

        if (minMinutesSinceLastSync < 0)
            return BadRequest("minMinutesSinceLastSync cannot be negative.");

        var result = await _preloadSyncService.RunAsync(
            new PreloadRunOptions
            {
                Season = season,
                MaxLeagues = maxLeagues,
                Force = force,
                StopOnRateLimit = stopOnRateLimit,
                MinMinutesSinceLastSync = minMinutesSinceLastSync,
                IncludeOdds = includeOdds
            },
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("historical")]
    public async Task<IActionResult> RunHistorical(
        [FromQuery] int fromSeason = 2023,
        [FromQuery] int? toSeason = null,
        [FromQuery] int? maxLeagueSeasons = null,
        [FromQuery] bool force = false,
        [FromQuery] bool stopOnRateLimit = true,
        [FromQuery] int minMinutesSinceLastSync = 1440,
        [FromQuery] bool includeOdds = false,
        [FromQuery] bool excludeAutomationWindow = true,
        CancellationToken cancellationToken = default)
    {
        if (toSeason.HasValue && toSeason.Value < fromSeason)
            return BadRequest("toSeason must be greater than or equal to fromSeason.");

        if (maxLeagueSeasons.HasValue && maxLeagueSeasons.Value <= 0)
            return BadRequest("maxLeagueSeasons must be greater than 0.");

        if (minMinutesSinceLastSync < 0)
            return BadRequest("minMinutesSinceLastSync cannot be negative.");

        var result = await _historicalBootstrapService.RunAsync(
            new HistoricalBootstrapRunOptions
            {
                FromSeason = fromSeason,
                ToSeason = toSeason,
                MaxLeagueSeasons = maxLeagueSeasons,
                Force = force,
                StopOnRateLimit = stopOnRateLimit,
                MinMinutesSinceLastSync = minMinutesSinceLastSync,
                IncludeOdds = includeOdds,
                ExcludeAutomationWindow = excludeAutomationWindow
            },
            cancellationToken);

        return Ok(result);
    }
}
