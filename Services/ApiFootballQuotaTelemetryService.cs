using System.Net.Http.Headers;

namespace SmartBets.Services;

public class ApiFootballQuotaTelemetryService
{
    private readonly object _sync = new();

    private int? _requestsDailyLimit;
    private int? _requestsDailyRemaining;
    private int? _requestsMinuteLimit;
    private int? _requestsMinuteRemaining;
    private DateTime? _lastObservedAtUtc;
    private DateTime? _lastRequestStartedAtUtc;

    public void MarkRequestStarted()
    {
        lock (_sync)
        {
            _lastRequestStartedAtUtc = DateTime.UtcNow;
        }
    }

    public void Record(HttpResponseHeaders headers)
    {
        lock (_sync)
        {
            _requestsDailyLimit = ReadIntHeader(headers, "x-ratelimit-requests-limit")
                                  ?? ReadIntHeader(headers, "X-RateLimit-Requests-Limit")
                                  ?? _requestsDailyLimit;

            _requestsDailyRemaining = ReadIntHeader(headers, "x-ratelimit-requests-remaining")
                                      ?? ReadIntHeader(headers, "X-RateLimit-Requests-Remaining")
                                      ?? _requestsDailyRemaining;

            _requestsMinuteLimit = ReadIntHeader(headers, "x-ratelimit-limit")
                                   ?? ReadIntHeader(headers, "X-RateLimit-Limit")
                                   ?? _requestsMinuteLimit;

            _requestsMinuteRemaining = ReadIntHeader(headers, "x-ratelimit-remaining")
                                       ?? ReadIntHeader(headers, "X-RateLimit-Remaining")
                                       ?? _requestsMinuteRemaining;

            _lastObservedAtUtc = DateTime.UtcNow;
        }
    }

    public ApiFootballQuotaSnapshot GetSnapshot(ApiFootballClientOptions options)
    {
        lock (_sync)
        {
            var mode = ResolveMode(
                _requestsDailyRemaining,
                _requestsMinuteRemaining,
                options);

            return new ApiFootballQuotaSnapshot
            {
                RequestsDailyLimit = _requestsDailyLimit,
                RequestsDailyRemaining = _requestsDailyRemaining,
                RequestsMinuteLimit = _requestsMinuteLimit,
                RequestsMinuteRemaining = _requestsMinuteRemaining,
                LastObservedAtUtc = _lastObservedAtUtc,
                LastRequestStartedAtUtc = _lastRequestStartedAtUtc,
                Mode = mode
            };
        }
    }

    public TimeSpan GetPreRequestDelay(ApiFootballClientOptions options)
    {
        var snapshot = GetSnapshot(options);
        var nowUtc = DateTime.UtcNow;
        var totalDelayMs = 0;

        if (snapshot.LastRequestStartedAtUtc.HasValue)
        {
            var elapsedMs = (int)(nowUtc - snapshot.LastRequestStartedAtUtc.Value).TotalMilliseconds;
            totalDelayMs += Math.Max(0, options.GetMinRequestSpacingMs() - elapsedMs);
        }

        totalDelayMs += snapshot.Mode switch
        {
            ApiFootballQuotaMode.Critical => options.GetCriticalQuotaDelayMs(),
            ApiFootballQuotaMode.Low => options.GetLowQuotaDelayMs(),
            _ => 0
        };

        return TimeSpan.FromMilliseconds(totalDelayMs);
    }

    private static int? ReadIntHeader(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
            return null;

        var raw = values.FirstOrDefault();
        return int.TryParse(raw, out var parsed)
            ? parsed
            : null;
    }

    private static ApiFootballQuotaMode ResolveMode(
        int? dailyRemaining,
        int? minuteRemaining,
        ApiFootballClientOptions options)
    {
        if ((dailyRemaining.HasValue && dailyRemaining.Value <= options.GetCriticalDailyRemainingThreshold()) ||
            (minuteRemaining.HasValue && minuteRemaining.Value <= options.GetCriticalMinuteRemainingThreshold()))
        {
            return ApiFootballQuotaMode.Critical;
        }

        if ((dailyRemaining.HasValue && dailyRemaining.Value <= options.GetLowDailyRemainingThreshold()) ||
            (minuteRemaining.HasValue && minuteRemaining.Value <= options.GetLowMinuteRemainingThreshold()))
        {
            return ApiFootballQuotaMode.Low;
        }

        return ApiFootballQuotaMode.Normal;
    }
}

public class ApiFootballQuotaSnapshot
{
    public int? RequestsDailyLimit { get; init; }
    public int? RequestsDailyRemaining { get; init; }
    public int? RequestsMinuteLimit { get; init; }
    public int? RequestsMinuteRemaining { get; init; }
    public DateTime? LastObservedAtUtc { get; init; }
    public DateTime? LastRequestStartedAtUtc { get; init; }
    public ApiFootballQuotaMode Mode { get; init; }
}

public enum ApiFootballQuotaMode
{
    Normal = 0,
    Low = 1,
    Critical = 2
}
