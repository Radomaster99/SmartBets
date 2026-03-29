using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Dtos;
using SmartBets.Services;

namespace SmartBets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    private readonly CountrySyncService _countrySyncService;
    private readonly AppDbContext _dbContext;

    public CountriesController(CountrySyncService countrySyncService, AppDbContext dbContext)
    {
        _countrySyncService = countrySyncService;
        _dbContext = dbContext;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var result = await _countrySyncService.SyncCountriesAsync(cancellationToken);

        return Ok(new
        {
            Message = "Countries synced successfully.",
            result.Processed,
            result.Inserted,
            result.Updated
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var countries = await _dbContext.Countries
            .OrderBy(x => x.Name)
            .Select(x => new CountryDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                FlagUrl = x.FlagUrl
            })
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }
}