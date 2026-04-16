using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize(Roles = "admin")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(JwtTokenResponseDto), StatusCodes.Status200OK)]
    public IActionResult IssueToken()
    {
        var token = _jwtTokenService.CreateToken(User);
        return Ok(token);
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated ?? false,
            authenticationType = User.Identity?.AuthenticationType,
            name = User.Identity?.Name,
            claims = User.Claims.Select(x => new { x.Type, x.Value })
        });
    }
}
