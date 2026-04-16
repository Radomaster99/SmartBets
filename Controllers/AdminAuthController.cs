using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly AdminAuthService _adminAuthService;

    public AdminAuthController(AdminAuthService adminAuthService)
    {
        _adminAuthService = adminAuthService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] AdminLoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_adminAuthService.IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "Admin auth is not configured."
            });
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                Message = "Username and password are required."
            });
        }

        if (!_adminAuthService.TryValidateCredentials(request.Username, request.Password, out var user))
        {
            return Unauthorized(new
            {
                Message = "Invalid username or password."
            });
        }

        var options = _adminAuthService.GetCurrentOptions();
        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.Add(options.GetSessionLifetime());
        var principal = _adminAuthService.CreatePrincipal(user, expiresAtUtc);

        await HttpContext.SignInAsync(
            AdminAuthService.AdminCookieScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                IssuedUtc = nowUtc,
                ExpiresUtc = expiresAtUtc
            });

        return Ok(BuildSessionDto(principal, configured: true));
    }

    [AllowAnonymous]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        return Ok(BuildSessionDto(User, _adminAuthService.IsConfigured()));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AdminAuthService.AdminCookieScheme);

        return Ok(new AdminSessionDto
        {
            Configured = _adminAuthService.IsConfigured(),
            Authenticated = false,
            AuthenticationType = null,
            Username = null,
            DisplayName = null,
            Roles = Array.Empty<string>(),
            AuthSource = null,
            SessionExpiresAtUtc = null
        });
    }

    private static AdminSessionDto BuildSessionDto(ClaimsPrincipal principal, bool configured)
    {
        var identity = principal.Identity;
        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new AdminSessionDto
        {
            Configured = configured,
            Authenticated = identity?.IsAuthenticated ?? false,
            AuthenticationType = identity?.AuthenticationType,
            Username = principal.FindFirst("admin_username")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            DisplayName = principal.FindFirst(ClaimTypes.Name)?.Value,
            Roles = roles,
            AuthSource = principal.FindFirst("auth_source")?.Value,
            SessionExpiresAtUtc = AdminAuthService.GetSessionExpiresAtUtc(principal)
        };
    }
}
