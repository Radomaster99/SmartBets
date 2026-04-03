using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartBets.Dtos;

namespace SmartBets.Services;

public class JwtTokenService
{
    private readonly IOptions<JwtAuthOptions> _options;
    private readonly IConfiguration _configuration;

    public JwtTokenService(
        IOptions<JwtAuthOptions> options,
        IConfiguration configuration)
    {
        _options = options;
        _configuration = configuration;
    }

    public JwtTokenResponseDto CreateToken(ClaimsPrincipal principal)
    {
        var options = _options.Value;
        var signingKeyBytes = ResolveSigningKey(options);

        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.AddMinutes(options.GetAccessTokenMinutes());
        var issuer = string.IsNullOrWhiteSpace(options.Issuer) ? "SmartBets" : options.Issuer.Trim();
        var audience = string.IsNullOrWhiteSpace(options.Audience) ? "SmartBets.Frontend" : options.Audience.Trim();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "smartbets-client"),
            new(JwtRegisteredClaimNames.UniqueName, principal.Identity?.Name ?? "SmartBets Client"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("auth_source", principal.Identity?.AuthenticationType ?? "unknown")
        };

        foreach (var role in principal.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.Ordinal))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(signingKeyBytes),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: nowUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtTokenResponseDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            ExpiresInSeconds = (int)Math.Round((expiresAtUtc - nowUtc).TotalSeconds),
            Issuer = issuer,
            Audience = audience
        };
    }

    private byte[] ResolveSigningKey(JwtAuthOptions options)
    {
        return JwtSigningKeyHelper.ResolveSigningKeyBytes(
            options.SigningKey,
            _configuration["ApiAuth:Token"]);
    }
}
