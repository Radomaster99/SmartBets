using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/content")]
public class AdminContentController : ControllerBase
{
    private readonly ContentStorageService _contentStorageService;

    public AdminContentController(ContentStorageService contentStorageService)
    {
        _contentStorageService = contentStorageService;
    }

    [HttpGet("bonus-codes")]
    public Task<IActionResult> GetBonusCodes(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.BonusCodes, cancellationToken);
    }

    [HttpPut("bonus-codes")]
    public Task<IActionResult> PutBonusCodes(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        return PutContentAsync(ContentStorageKeys.BonusCodes, payload, cancellationToken);
    }

    [HttpGet("hero-banners")]
    public Task<IActionResult> GetHeroBanners(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.HeroBanners, cancellationToken);
    }

    [HttpPut("hero-banners")]
    public Task<IActionResult> PutHeroBanners(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        return PutContentAsync(ContentStorageKeys.HeroBanners, payload, cancellationToken);
    }

    [HttpGet("side-ads")]
    public Task<IActionResult> GetSideAds(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.SideAds, cancellationToken);
    }

    [HttpPut("side-ads")]
    public Task<IActionResult> PutSideAds(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        return PutContentAsync(ContentStorageKeys.SideAds, payload, cancellationToken);
    }

    [HttpGet("popular-leagues")]
    public Task<IActionResult> GetPopularLeagues(CancellationToken cancellationToken = default)
    {
        return GetContentAsync(ContentStorageKeys.PopularLeagues, cancellationToken);
    }

    [HttpPut("popular-leagues")]
    public Task<IActionResult> PutPopularLeagues(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        return PutContentAsync(ContentStorageKeys.PopularLeagues, payload, cancellationToken);
    }

    private async Task<IActionResult> GetContentAsync(
        string contentKey,
        CancellationToken cancellationToken)
    {
        var payloadJson = await _contentStorageService.GetPayloadJsonAsync(contentKey, cancellationToken);
        return Content(payloadJson, "application/json");
    }

    private async Task<IActionResult> PutContentAsync(
        string contentKey,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new
            {
                Message = "Payload must be a JSON array."
            });
        }

        var payloadJson = await _contentStorageService.UpsertArrayPayloadAsync(
            contentKey,
            payload,
            ResolveUpdatedBy(),
            cancellationToken);

        return Content(payloadJson, "application/json");
    }

    private string? ResolveUpdatedBy()
    {
        return User.FindFirst("admin_username")?.Value
               ?? User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
