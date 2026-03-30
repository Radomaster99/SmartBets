using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;

    public DebugController(IConfiguration configuration, AppDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    [HttpGet("config")]
    public IActionResult Config()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var apiBaseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        return Ok(new
        {
            ConnectionStringExists = !string.IsNullOrWhiteSpace(connectionString),
            ConnectionStringPreview = string.IsNullOrWhiteSpace(connectionString)
                ? null
                : connectionString.Length > 50
                    ? connectionString.Substring(0, 50) + "..."
                    : connectionString,
            ApiBaseUrl = apiBaseUrl,
            ApiKeyExists = !string.IsNullOrWhiteSpace(apiKey)
        });
    }

    [HttpGet("db")]
    public async Task<IActionResult> Db(CancellationToken cancellationToken)
    {
        return Ok(new
        {
            CanConnect = await _dbContext.Database.CanConnectAsync(cancellationToken),
            Countries = await _dbContext.Countries.CountAsync(cancellationToken),
            Leagues = await _dbContext.Leagues.CountAsync(cancellationToken),
            Teams = await _dbContext.Teams.CountAsync(cancellationToken),
            Fixtures = await _dbContext.Fixtures.CountAsync(cancellationToken),
            SupportedLeagues = await _dbContext.SupportedLeagues.CountAsync(cancellationToken)
        });
    }
}