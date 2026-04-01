using SmartBets.Models.ApiFootball;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace SmartBets.Services;

public class FootballApiService
{
    private static readonly SemaphoreSlim RequestGate = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<ApiFootballClientOptions> _optionsMonitor;
    private readonly ApiFootballQuotaTelemetryService _quotaTelemetryService;
    private readonly ILogger<FootballApiService> _logger;

    public FootballApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptionsMonitor<ApiFootballClientOptions> optionsMonitor,
        ApiFootballQuotaTelemetryService quotaTelemetryService,
        ILogger<FootballApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _optionsMonitor = optionsMonitor;
        _quotaTelemetryService = quotaTelemetryService;
        _logger = logger;
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

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<ApiFootballCountriesResponse>(
            raw,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

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

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);
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

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

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

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

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

            using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

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

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

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

    public async Task<List<ApiFootballFixtureItem>> GetLiveFixturesAsync(
        IReadOnlyCollection<long>? leagueIds = null,
        CancellationToken cancellationToken = default)
    {
        var liveScope = leagueIds is { Count: > 0 }
            ? string.Join('-', leagueIds.Distinct().OrderBy(x => x))
            : "all";

        var result = await SendGetAsync<ApiFootballFixturesResponse>(
            $"/fixtures?live={liveScope}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureItem>();
    }

    public async Task<List<ApiFootballFixtureItem>> GetFixturesByIdsAsync(
        IReadOnlyCollection<long> fixtureIds,
        CancellationToken cancellationToken = default)
    {
        if (fixtureIds.Count == 0)
            return new List<ApiFootballFixtureItem>();

        var ids = string.Join('-', fixtureIds.Distinct().OrderBy(x => x));
        var result = await SendGetAsync<ApiFootballFixturesResponse>(
            $"/fixtures?ids={ids}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureItem>();
    }

    public async Task<List<ApiFootballFixtureEventItem>> GetFixtureEventsAsync(
        long fixtureId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballFixtureEventsResponse>(
            $"/fixtures/events?fixture={fixtureId}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureEventItem>();
    }

    public async Task<List<ApiFootballFixtureLineupItem>> GetFixtureLineupsAsync(
        long fixtureId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballFixtureLineupsResponse>(
            $"/fixtures/lineups?fixture={fixtureId}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureLineupItem>();
    }

    public async Task<List<ApiFootballFixtureStatisticsItem>> GetFixtureStatisticsAsync(
        long fixtureId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballFixtureStatisticsResponse>(
            $"/fixtures/statistics?fixture={fixtureId}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixtureStatisticsItem>();
    }

    public async Task<List<ApiFootballFixturePlayersTeamItem>> GetFixturePlayersAsync(
        long fixtureId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballFixturePlayersResponse>(
            $"/fixtures/players?fixture={fixtureId}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballFixturePlayersTeamItem>();
    }

    public async Task<ApiFootballPredictionItem?> GetPredictionAsync(
        long fixtureId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballPredictionsResponse>(
            $"/predictions?fixture={fixtureId}",
            cancellationToken);

        return result?.Response?.FirstOrDefault();
    }

    public async Task<List<ApiFootballInjuryItem>> GetFixtureInjuriesAsync(
        long fixtureId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballInjuriesResponse>(
            $"/injuries?fixture={fixtureId}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballInjuryItem>();
    }

    public async Task<ApiFootballTeamStatisticsItem?> GetTeamStatisticsAsync(
        long teamId,
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

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/teams/statistics?team={teamId}&league={leagueId}&season={season}");
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("API-Football rate limit reached. Try again later or reduce sync scope.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        try
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("response", out var responseElement))
                return null;

            return responseElement.ValueKind switch
            {
                JsonValueKind.Object => DeserializeTeamStatistics(responseElement),
                JsonValueKind.Array => DeserializeTeamStatisticsArray(responseElement),
                JsonValueKind.Null => null,
                _ => null
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialize team statistics for team {TeamId}, league {LeagueId}, season {Season}. Treating as no data.",
                teamId,
                leagueId,
                season);

            return null;
        }
    }

    public async Task<List<string>> GetRoundsAsync(
        long leagueId,
        int season,
        bool currentOnly = false,
        CancellationToken cancellationToken = default)
    {
        var currentQuery = currentOnly ? "&current=true" : string.Empty;

        var result = await SendGetAsync<ApiFootballRoundsResponse>(
            $"/fixtures/rounds?league={leagueId}&season={season}{currentQuery}",
            cancellationToken);

        return result?.Response ?? new List<string>();
    }

    public async Task<List<ApiFootballTopPlayerItem>> GetTopScorersAsync(
        long leagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballTopPlayersResponse>(
            $"/players/topscorers?league={leagueId}&season={season}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballTopPlayerItem>();
    }

    public async Task<List<ApiFootballTopPlayerItem>> GetTopAssistsAsync(
        long leagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballTopPlayersResponse>(
            $"/players/topassists?league={leagueId}&season={season}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballTopPlayerItem>();
    }

    public async Task<List<ApiFootballTopPlayerItem>> GetTopYellowCardsAsync(
        long leagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballTopPlayersResponse>(
            $"/players/topyellowcards?league={leagueId}&season={season}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballTopPlayerItem>();
    }

    public async Task<List<ApiFootballTopPlayerItem>> GetTopRedCardsAsync(
        long leagueId,
        int season,
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballTopPlayersResponse>(
            $"/players/topredcards?league={leagueId}&season={season}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballTopPlayerItem>();
    }

    public async Task<List<ApiFootballLiveOddsFixtureItem>> GetLiveOddsAsync(
        long? fixtureId = null,
        long? leagueId = null,
        long? betId = null,
        long? bookmakerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (fixtureId.HasValue)
            query.Add($"fixture={fixtureId.Value}");

        if (leagueId.HasValue)
            query.Add($"league={leagueId.Value}");

        if (betId.HasValue)
            query.Add($"bet={betId.Value}");

        if (bookmakerId.HasValue)
            query.Add($"bookmaker={bookmakerId.Value}");

        var suffix = query.Count == 0
            ? string.Empty
            : $"?{string.Join('&', query)}";

        var result = await SendGetAsync<ApiFootballLiveOddsResponse>(
            $"/odds/live{suffix}",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballLiveOddsFixtureItem>();
    }

    public async Task<List<ApiFootballLiveBetTypeItem>> GetLiveOddsBetTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendGetAsync<ApiFootballLiveBetTypesResponse>(
            "/odds/live/bets",
            cancellationToken);

        return result?.Response ?? new List<ApiFootballLiveBetTypeItem>();
    }

    private async Task<T?> SendGetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["ApiFootball:BaseUrl"];
        var apiKey = _configuration["ApiFootball:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ApiFootball:BaseUrl is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiFootball:ApiKey is missing.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}{relativeUrl}");
        request.Headers.Add("x-apisports-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await SendAsyncWithQuotaAwarenessAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("API-Football rate limit reached. Try again later or reduce sync scope.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);
    }

    private static ApiFootballTeamStatisticsItem? DeserializeTeamStatistics(JsonElement element)
    {
        return JsonSerializer.Deserialize<ApiFootballTeamStatisticsItem>(
            element.GetRawText(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    private static ApiFootballTeamStatisticsItem? DeserializeTeamStatisticsArray(JsonElement element)
    {
        if (element.GetArrayLength() == 0)
            return null;

        var first = element[0];
        return first.ValueKind == JsonValueKind.Object
            ? DeserializeTeamStatistics(first)
            : null;
    }

    private async Task<HttpResponseMessage> SendAsyncWithQuotaAwarenessAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await RequestGate.WaitAsync(cancellationToken);

        try
        {
            var options = _optionsMonitor.CurrentValue;
            var preRequestDelay = _quotaTelemetryService.GetPreRequestDelay(options);

            if (preRequestDelay > TimeSpan.Zero)
            {
                await Task.Delay(preRequestDelay, cancellationToken);
            }

            _quotaTelemetryService.MarkRequestStarted();

            var response = await _httpClient.SendAsync(request, cancellationToken);
            _quotaTelemetryService.Record(response.Headers);

            var snapshot = _quotaTelemetryService.GetSnapshot(options);
            if (snapshot.Mode == ApiFootballQuotaMode.Critical)
            {
                _logger.LogWarning(
                    "API-Football quota is in CRITICAL mode. DailyRemaining={DailyRemaining}, MinuteRemaining={MinuteRemaining}",
                    snapshot.RequestsDailyRemaining,
                    snapshot.RequestsMinuteRemaining);
            }
            else if (snapshot.Mode == ApiFootballQuotaMode.Low)
            {
                _logger.LogInformation(
                    "API-Football quota is in LOW mode. DailyRemaining={DailyRemaining}, MinuteRemaining={MinuteRemaining}",
                    snapshot.RequestsDailyRemaining,
                    snapshot.RequestsMinuteRemaining);
            }

            return response;
        }
        finally
        {
            RequestGate.Release();
        }
    }
}
