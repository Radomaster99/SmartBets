using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartBets.Models.TheOddsApi;

namespace SmartBets.Services;

public class TheOddsApiService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TheOddsApiOptions> _optionsMonitor;
    private readonly ILogger<TheOddsApiService> _logger;

    public TheOddsApiService(
        HttpClient httpClient,
        IOptionsMonitor<TheOddsApiOptions> optionsMonitor,
        ILogger<TheOddsApiService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<List<TheOddsApiOddsItem>> GetLiveH2HOddsAsync(
        string sportKey,
        DateTime? commenceTimeFromUtc = null,
        DateTime? commenceTimeToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.IsConfigured())
            throw new InvalidOperationException("TheOddsApi configuration is missing or disabled.");

        if (string.IsNullOrWhiteSpace(sportKey))
            throw new InvalidOperationException("The Odds API sport key is missing.");

        var relativeUrl =
            $"/sports/{Uri.EscapeDataString(sportKey)}/odds" +
            $"?apiKey={Uri.EscapeDataString(options.ApiKey)}" +
            $"&regions={Uri.EscapeDataString(options.GetRegions())}" +
            $"&markets={Uri.EscapeDataString(options.GetMarketKey())}" +
            $"&oddsFormat={Uri.EscapeDataString(options.GetOddsFormat())}" +
            $"&dateFormat={Uri.EscapeDataString(options.GetDateFormat())}";

        if (commenceTimeFromUtc.HasValue)
        {
            relativeUrl += $"&commenceTimeFrom={Uri.EscapeDataString(commenceTimeFromUtc.Value.ToUniversalTime().ToString("O"))}";
        }

        if (commenceTimeToUtc.HasValue)
        {
            relativeUrl += $"&commenceTimeTo={Uri.EscapeDataString(commenceTimeToUtc.Value.ToUniversalTime().ToString("O"))}";
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{options.GetBaseUrl()}{relativeUrl}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("The Odds API rate limit reached. Try again later.");

        response.EnsureSuccessStatusCode();

        var requestsRemaining = response.Headers.TryGetValues("x-requests-remaining", out var remainingValues)
            ? remainingValues.FirstOrDefault()
            : null;
        var requestsLast = response.Headers.TryGetValues("x-requests-last", out var lastValues)
            ? lastValues.FirstOrDefault()
            : null;

        if (!string.IsNullOrWhiteSpace(requestsRemaining) || !string.IsNullOrWhiteSpace(requestsLast))
        {
            _logger.LogInformation(
                "The Odds API request completed for sport {SportKey}. Remaining={Remaining}, LastCost={LastCost}",
                sportKey,
                requestsRemaining,
                requestsLast);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<List<TheOddsApiOddsItem>>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result ?? new List<TheOddsApiOddsItem>();
    }

    public async Task<List<TheOddsApiSport>> GetSportsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.IsConfigured())
            throw new InvalidOperationException("TheOddsApi configuration is missing or disabled.");

        var relativeUrl =
            $"/sports?apiKey={Uri.EscapeDataString(options.ApiKey)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{options.GetBaseUrl()}{relativeUrl}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("The Odds API rate limit reached. Try again later.");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<List<TheOddsApiSport>>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result ?? new List<TheOddsApiSport>();
    }

    public async Task<List<TheOddsApiSport>> GetActiveSoccerSportsAsync(
        CancellationToken cancellationToken = default)
    {
        var sports = await GetSportsAsync(cancellationToken);

        return sports
            .Where(x =>
                x.Active &&
                !x.HasOutrights &&
                x.Key.StartsWith("soccer_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key)
            .ToList();
    }
}
