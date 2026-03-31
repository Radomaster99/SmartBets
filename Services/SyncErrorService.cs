using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class SyncErrorService
{
    private readonly AppDbContext _dbContext;

    public SyncErrorService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordAsync(
        string entityType,
        string operation,
        string source,
        string errorMessage,
        long? leagueApiId = null,
        int? season = null,
        CancellationToken cancellationToken = default)
    {
        _dbContext.SyncErrors.Add(new SyncError
        {
            EntityType = Normalize(entityType, "unknown"),
            Operation = Normalize(operation, "unknown"),
            Source = Normalize(source, "unknown"),
            ErrorMessage = Truncate(errorMessage, 2000),
            LeagueApiId = leagueApiId,
            Season = season,
            OccurredAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TryRecordRequestFailureAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveRequest(context.Request.Path.Value, out var entityType, out var operation))
            return;

        long? leagueApiId = null;
        int? season = null;

        if (long.TryParse(context.Request.Query["leagueId"].FirstOrDefault(), out var parsedLeagueApiId))
            leagueApiId = parsedLeagueApiId;
        else if (TryResolveLeagueApiIdFromPath(context.Request.Path.Value, out parsedLeagueApiId))
            leagueApiId = parsedLeagueApiId;

        if (int.TryParse(context.Request.Query["season"].FirstOrDefault(), out var parsedSeason))
            season = parsedSeason;

        await RecordAsync(
            entityType,
            operation,
            "request",
            exception.Message,
            leagueApiId,
            season,
            cancellationToken);
    }

    private static bool TryResolveRequest(
        string? path,
        out string entityType,
        out string operation)
    {
        var normalizedPath = path?.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(normalizedPath) &&
            normalizedPath.StartsWith("/api/fixtures/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith("/sync-match-center", StringComparison.Ordinal))
        {
            entityType = "match_center";
            operation = "sync";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedPath) &&
            normalizedPath.StartsWith("/api/fixtures/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith("/sync-preview", StringComparison.Ordinal))
        {
            entityType = "preview";
            operation = "sync";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedPath) &&
            normalizedPath.StartsWith("/api/leagues/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith("/analytics/sync", StringComparison.Ordinal))
        {
            entityType = "league_analytics";
            operation = "sync";
            return true;
        }

        var resolved = normalizedPath switch
        {
            "/api/countries/sync" => ("countries", "sync"),
            "/api/leagues/sync" => ("leagues", "sync"),
            "/api/teams/sync" => ("teams", "sync"),
            "/api/teams/statistics/sync" => ("team_statistics", "sync"),
            "/api/fixtures/sync" => ("fixtures_full", "sync"),
            "/api/fixtures/sync-upcoming" => ("fixtures_upcoming", "sync"),
            "/api/fixtures/sync-live-match-center" => ("match_center_live", "sync"),
            "/api/fixtures/sync-upcoming-previews" => ("preview_batch", "sync"),
            "/api/leagues/analytics/sync" => ("league_analytics_batch", "sync"),
            "/api/standings/sync" => ("standings", "sync"),
            "/api/bookmakers/sync" => ("bookmakers", "sync"),
            "/api/odds/sync" => ("odds", "sync"),
            "/api/odds/analytics/rebuild" => ("odds_analytics", "rebuild"),
            "/api/preload/run" => ("preload", "run"),
            _ => default
        };

        if (string.IsNullOrWhiteSpace(resolved.Item1) || string.IsNullOrWhiteSpace(resolved.Item2))
        {
            entityType = string.Empty;
            operation = string.Empty;
            return false;
        }

        entityType = resolved.Item1;
        operation = resolved.Item2;
        return true;
    }

    private static bool TryResolveLeagueApiIdFromPath(string? path, out long leagueApiId)
    {
        leagueApiId = 0;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 4 &&
            string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "leagues", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(segments[2], out leagueApiId))
        {
            return true;
        }

        return false;
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown sync error.";

        var normalizedValue = value.Trim();
        return normalizedValue.Length <= maxLength
            ? normalizedValue
            : normalizedValue[..maxLength];
    }
}
