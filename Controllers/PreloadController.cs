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
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var result = await _preloadSyncService.RunAsync(cancellationToken);
        return Ok(result);
    }
}