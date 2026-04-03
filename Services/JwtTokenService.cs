using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
        var signingKey = ResolveSigningKey(options);
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("JWT signing key is missing. Configure JwtAuth:SigningKey or ApiAuth:Token.");
        }

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
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
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

    private string ResolveSigningKey(JwtAuthOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SigningKey))
            return options.SigningKey.Trim();

        return _configuration["ApiAuth:Token"]?.Trim() ?? string.Empty;
    }
}
