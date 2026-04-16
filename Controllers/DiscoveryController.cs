using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
[Route("api/[controller]")]
public class DiscoveryController : ControllerBase
{
    private readonly DiscoveryService _discoveryService;

    public DiscoveryController(DiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [HttpGet("league-coverage")]
    public async Task<IActionResult> GetLeagueCoverage(
        [FromQuery] int season,
        [FromQuery] int maxLeaguesToCheck = 10,
        CancellationToken cancellationToken = default)
    {
        if (maxLeaguesToCheck <= 0)
            return BadRequest("maxLeaguesToCheck must be greater than 0.");

        if (maxLeaguesToCheck > 25)
            maxLeaguesToCheck = 25;

        var result = await _discoveryService.CheckLeagueCoverageAsync(
            season,
            maxLeaguesToCheck,
            cancellationToken);

        return Ok(result);
    }
}
