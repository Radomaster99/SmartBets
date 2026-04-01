using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreloadController : ControllerBase
{
    private readonly PreloadSyncService _preloadSyncService;

    public PreloadController(PreloadSyncService preloadSyncService)
    {
        _preloadSyncService = preloadSyncService;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(
        [FromQuery] int? season,
        [FromQuery] int? maxLeagues,
        [FromQuery] bool force = false,
        [FromQuery] bool stopOnRateLimit = true,
        [FromQuery] int minMinutesSinceLastSync = 180,
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
                MinMinutesSinceLastSync = minMinutesSinceLastSync
            },
            cancellationToken);

        return Ok(result);
    }
}
