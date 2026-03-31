using SmartBets.Models.ApiFootball;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SmartBets.Services;

public class FootballApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public FootballApiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<ApiFootballCountryItem>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/countries");
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine($"[API-FOOTBALL] Countries status: {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine($"[API-FOOTBALL] Countries raw response: {raw}");

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<ApiFootballCountriesResponse>(
            raw,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Console.WriteLine($"[API-FOOTBALL] Countries parsed count: {result?.Response?.Count ?? 0}");

        return result?.Response ?? new List<ApiFootballCountryItem>();
    }
    public async Task<List<ApiFootballStandingItem>> GetStandingsAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        var url = $"{baseUrl.TrimEnd('/')}/standings?league={leagueId}&season={season}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("API-Football rate limit reached. Try again later.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiFootballStandingsResponse>(
            stream,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        if (result?.Response == null || result.Response.Count == 0)
            return new List<ApiFootballStandingItem>();

        return result.Response
            .SelectMany(x => x.League.Standings)
            .SelectMany(x => x)
            .ToList();
    }
    public async Task<List<ApiFootballLeagueItem>> GetLeaguesAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl!.TrimEnd('/')}/leagues");
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiFootballLeaguesResponse>(
            stream,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result?.Response ?? new List<ApiFootballLeagueItem>();
    }
    public async Task<List<ApiFootballTeamItem>> GetTeamsAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        var url = $"{baseUrl.TrimEnd('/')}/teams?league={leagueId}&season={season}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("API-Football rate limit reached. Try again later or use a smaller sync scope.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiFootballTeamsResponse>(
            stream,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result?.Response ?? new List<ApiFootballTeamItem>();
    }
    public async Task<List<ApiFootballFixtureItem>> GetFixturesAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        var url = $"{baseUrl.TrimEnd('/')}/fixtures?league={leagueId}&season={season}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("API-Football rate limit reached. Try again later or use a smaller sync scope.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiFootballFixturesResponse>(
            stream,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureItem>();
    }
    public async Task<List<ApiFootballOddsFixtureItem>> GetOddsAsync(long leagueId, int season, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        var allItems = new List<ApiFootballOddsFixtureItem>();
        var currentPage = 1;
        var totalPages = 1;

        do
        {
            var url = $"{baseUrl.TrimEnd('/')}/odds?league={leagueId}&season={season}&page={currentPage}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-apisports-key", apiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException("API-Football rate limit reached. Try again later or use a smaller sync scope.");
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var result = await System.Text.Json.JsonSerializer.DeserializeAsync<ApiFootballOddsResponse>(
                stream,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            if (result?.Response is { Count: > 0 })
            {
                allItems.AddRange(result.Response);
            }

            totalPages = Math.Max(result?.Paging?.Total ?? 1, 1);
            currentPage++;
        }
        while (currentPage <= totalPages);

        return allItems;
    }
    public async Task<List<ApiFootballFixtureItem>> GetUpcomingFixturesAsync(
    long leagueId,
    int season,
    CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        var url = $"{baseUrl.TrimEnd('/')}/fixtures?league={leagueId}&season={season}&status=NS";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("API-Football rate limit reached. Try again later or reduce sync scope.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await JsonSerializer.DeserializeAsync<ApiFootballFixturesResponse>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureItem>();
    }
}
