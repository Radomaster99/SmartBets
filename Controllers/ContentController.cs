using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/content")]
public class ContentController : ControllerBase
{
    private readonly ContentStorageService _contentStorageService;

    public ContentController(ContentStorageService contentStorageService)
    {
        _contentStorageService = contentStorageService;
    }

    [HttpGet("bonus-codes")]
    public Task<IActionResult> GetBonusCodes(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.BonusCodes, cancellationToken);
    }

    [HttpGet("hero-banners")]
    public Task<IActionResult> GetHeroBanners(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.HeroBanners, cancellationToken);
    }

    [HttpGet("side-ads")]
    public Task<IActionResult> GetSideAds(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.SideAds, cancellationToken);
    }

    [HttpGet("popular-leagues")]
    public Task<IActionResult> GetPopularLeagues(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.PopularLeagues, cancellationToken);
    }

    private async Task<IActionResult> GetContentAsync(
        string contentKey,
        CancellationToken cancellationToken)
    {
        var payloadJson = await _contentStorageService.GetPayloadJsonAsync(contentKey, cancellationToken);
        return Content(payloadJson, "application/json");
    }
}
