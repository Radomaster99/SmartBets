using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartBets.Data;
using SmartBets.Entities;
using SmartBets.Models.TheOddsApi;

namespace SmartBets.Services;

public class TheOddsSportKeyResolverService
{
    private const string UnresolvedCacheValue = "__UNRESOLVED__";
    private const string SportsCacheKey = "theodds:active-soccer-sports";

    private static readonly TimeSpan SportsCacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan ResolvedCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan UnresolvedCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan UnresolvedRecheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan LastUsedUpdateInterval = TimeSpan.FromMinutes(15);

    private static readonly Dictionary<string, string> KnownLeagueAliases = new(StringComparer.Ordinal)
    {
        ["ENGLAND|PREMIERLEAGUE"] = "soccer_epl",
        ["ENGLAND|CHAMPIONSHIP"] = "soccer_efl_champ",
        ["ENGLAND|LEAGUE1"] = "soccer_england_league1",
        ["ENGLAND|LEAGUE2"] = "soccer_england_league2",
        ["SCOTLAND|PREMIERSHIP"] = "soccer_spl",
        ["SCOTLAND|SCOTTISHPREMIERSHIP"] = "soccer_spl",
        ["USA|MLS"] = "soccer_usa_mls",
        ["USA|MAJORLEAGUESOCCER"] = "soccer_usa_mls",
        ["UNITEDSTATES|MLS"] = "soccer_usa_mls",
        ["UNITEDSTATES|MAJORLEAGUESOCCER"] = "soccer_usa_mls",
        ["BRAZIL|SERIEA"] = "soccer_brazil_campeonato",
        ["BRAZIL|BRASILEIRAOSERIEA"] = "soccer_brazil_campeonato",
        ["BRAZIL|CAMPEONATOBRASILEIROSERIEA"] = "soccer_brazil_campeonato",
        ["SPAIN|LALIGA"] = "soccer_spain_la_liga",
        ["SPAIN|SEGUNDADIVISION"] = "soccer_spain_segunda_division"
    };

    private static readonly Dictionary<string, string[]> CountryAliases = new(StringComparer.Ordinal)
    {
        ["ARGENTINA"] = ["ARGENTINA", "ARGENTINE"],
        ["AUSTRALIA"] = ["AUSTRALIA", "AUSTRALIAN", "AUSSIE"],
        ["AUSTRIA"] = ["AUSTRIA", "AUSTRIAN"],
        ["BELGIUM"] = ["BELGIUM", "BELGIAN"],
        ["BRAZIL"] = ["BRAZIL", "BRASIL", "BRASILEIRAO", "BRASILEIRÃO"],
        ["CHILE"] = ["CHILE", "CHILEAN"],
        ["CHINA"] = ["CHINA", "CHINESE"],
        ["DENMARK"] = ["DENMARK", "DANISH"],
        ["ENGLAND"] = ["ENGLAND", "ENGLISH", "EPL", "EFL"],
        ["FINLAND"] = ["FINLAND", "FINNISH"],
        ["FRANCE"] = ["FRANCE", "FRENCH"],
        ["GERMANY"] = ["GERMANY", "GERMAN", "DFB"],
        ["GREECE"] = ["GREECE", "GREEK"],
        ["IRELAND"] = ["IRELAND", "IRISH"],
        ["ITALY"] = ["ITALY", "ITALIAN"],
        ["JAPAN"] = ["JAPAN", "JAPANESE"],
        ["KOREA"] = ["KOREA", "KOREAN"],
        ["MEXICO"] = ["MEXICO", "MEXICAN"],
        ["NETHERLANDS"] = ["NETHERLANDS", "DUTCH"],
        ["NORWAY"] = ["NORWAY", "NORWEGIAN"],
        ["POLAND"] = ["POLAND", "POLISH"],
        ["PORTUGAL"] = ["PORTUGAL", "PORTUGUESE", "PORTUGESE"],
        ["RUSSIA"] = ["RUSSIA", "RUSSIAN"],
        ["SAUDIARABIA"] = ["SAUDIARABIA", "SAUDI"],
        ["SCOTLAND"] = ["SCOTLAND", "SCOTTISH", "SPL"],
        ["SPAIN"] = ["SPAIN", "SPANISH"],
        ["SWEDEN"] = ["SWEDEN", "SWEDISH"],
        ["SWITZERLAND"] = ["SWITZERLAND", "SWISS"],
        ["TURKEY"] = ["TURKEY", "TURKISH"],
        ["UNITEDSTATES"] = ["UNITEDSTATES", "USA", "US", "MLS", "MAJORLEAGUESOCCER"]
    };

    private readonly AppDbContext _dbContext;
    private readonly TheOddsApiService _apiService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TheOddsSportKeyResolverService> _logger;

    public TheOddsSportKeyResolverService(
        AppDbContext dbContext,
        TheOddsApiService apiService,
        IMemoryCache memoryCache,
        ILogger<TheOddsSportKeyResolverService> logger)
    {
        _dbContext = dbContext;
        _apiService = apiService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<TheOddsSportKeyResolutionResult> ResolveAsync(
        long leagueApiId,
        int season,
        string? configuredSportKey,
        IReadOnlyCollection<TheOddsFixtureLookupContext>? fixtures,
        TimeSpan matchTolerance,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => x.ApiLeagueId == leagueApiId && x.Season == season)
            .Select(x => new LeagueLookupContext
            {
                LeagueApiId = x.ApiLeagueId,
                Season = x.Season,
                LeagueName = x.Name,
                CountryName = x.Country.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (league is null)
            return new TheOddsSportKeyResolutionResult();

        if (!string.IsNullOrWhiteSpace(configuredSportKey))
        {
            var resolvedSportKey = configuredSportKey.Trim();
            await UpsertMappingAsync(
                league,
                resolvedSportKey,
                "configured_override",
                100,
                isVerified: true,
                notes: "Configured through TheOddsApi:LeagueSportKeys override.",
                touchLastUsed: true,
                cancellationToken);

            CacheResolved(leagueApiId, resolvedSportKey);

            return new TheOddsSportKeyResolutionResult
            {
                SportKey = resolvedSportKey,
                Source = "configured_override",
                Confidence = 100
            };
        }

        var cacheKey = BuildCacheKey(leagueApiId);
        if (_memoryCache.TryGetValue<string>(cacheKey, out var cachedSportKey))
        {
            return new TheOddsSportKeyResolutionResult
            {
                SportKey = string.Equals(cachedSportKey, UnresolvedCacheValue, StringComparison.Ordinal)
                    ? null
                    : cachedSportKey,
                Source = "memory_cache"
            };
        }

        var existingMapping = await _dbContext.TheOddsLeagueMappings
            .FirstOrDefaultAsync(x => x.ApiFootballLeagueId == leagueApiId, cancellationToken);

        if (existingMapping is not null)
        {
            if (!string.IsNullOrWhiteSpace(existingMapping.TheOddsSportKey))
            {
                if (!existingMapping.LastUsedAtUtc.HasValue ||
                    DateTime.UtcNow - existingMapping.LastUsedAtUtc.Value >= LastUsedUpdateInterval)
                {
                    existingMapping.LastUsedAtUtc = DateTime.UtcNow;
                    existingMapping.UpdatedAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                CacheResolved(leagueApiId, existingMapping.TheOddsSportKey);

                return new TheOddsSportKeyResolutionResult
                {
                    SportKey = existingMapping.TheOddsSportKey,
                    Source = "db_mapping",
                    Confidence = existingMapping.Confidence,
                    IsVerified = existingMapping.IsVerified
                };
            }

            if (existingMapping.LastResolvedAtUtc.HasValue &&
                DateTime.UtcNow - existingMapping.LastResolvedAtUtc.Value < UnresolvedRecheckInterval)
            {
                CacheUnresolved(leagueApiId);

                return new TheOddsSportKeyResolutionResult
                {
                    Source = "db_unresolved",
                    Confidence = existingMapping.Confidence,
                    IsVerified = existingMapping.IsVerified
                };
            }
        }

        var aliasMatch = TryResolveFromKnownAliases(league);
        if (!string.IsNullOrWhiteSpace(aliasMatch))
        {
            await UpsertMappingAsync(
                league,
                aliasMatch,
                "known_alias",
                100,
                isVerified: false,
                notes: "Resolved from built-in known alias map.",
                touchLastUsed: true,
                cancellationToken);

            CacheResolved(leagueApiId, aliasMatch);

            return new TheOddsSportKeyResolutionResult
            {
                SportKey = aliasMatch,
                Source = "known_alias",
                Confidence = 100
            };
        }

        var sportsLoad = await GetActiveSoccerSportsAsync(cancellationToken);
        var sports = sportsLoad.Sports;
        var requestsUsed = sportsLoad.RequestsUsed;

        var candidates = BuildCandidates(league, sports);

        if (candidates.Count > 0)
        {
            var best = candidates[0];
            var secondScore = candidates.Count > 1 ? candidates[1].Score : int.MinValue;
            if (best.Score >= 100 && best.Score - secondScore >= 15)
            {
                await UpsertMappingAsync(
                    league,
                    best.Sport.Key,
                    "heuristic",
                    Math.Min(best.Score, 100),
                    isVerified: false,
                    notes: $"Resolved heuristically from league/country names. Score={best.Score}.",
                    touchLastUsed: true,
                    cancellationToken);

                CacheResolved(leagueApiId, best.Sport.Key);

                _logger.LogInformation(
                    "Resolved The Odds sport key heuristically for league {LeagueApiId}/{Season}: {SportKey} (score {Score})",
                    leagueApiId,
                    season,
                    best.Sport.Key,
                    best.Score);

                return new TheOddsSportKeyResolutionResult
                {
                    SportKey = best.Sport.Key,
                    RequestsUsed = requestsUsed,
                    Source = "heuristic",
                    Confidence = Math.Min(best.Score, 100)
                };
            }
        }

        if (fixtures is not null && fixtures.Count > 0 && candidates.Count > 0)
        {
            var discoveryCandidates = candidates.Take(3).ToList();
            var commenceTimeFromUtc = fixtures.Min(x => x.KickoffAtUtc).Subtract(matchTolerance);
            var commenceTimeToUtc = fixtures.Max(x => x.KickoffAtUtc).Add(matchTolerance);

            SportCandidateScore? bestMatch = null;
            var bestMatchCount = 0;

            foreach (var candidate in discoveryCandidates)
            {
                var providerRows = await _apiService.GetLiveH2HOddsAsync(
                    candidate.Sport.Key,
                    commenceTimeFromUtc,
                    commenceTimeToUtc,
                    cancellationToken);
                requestsUsed++;

                var matchCount = providerRows.Count(x => MatchesAnyFixture(x, fixtures, matchTolerance));
                if (matchCount <= 0)
                    continue;

                if (matchCount > bestMatchCount)
                {
                    bestMatch = candidate;
                    bestMatchCount = matchCount;
                }
            }

            if (bestMatch is not null)
            {
                var discoveryConfidence = Math.Min(bestMatch.Score + (bestMatchCount * 5), 100);

                await UpsertMappingAsync(
                    league,
                    bestMatch.Sport.Key,
                    "provider_discovery",
                    discoveryConfidence,
                    isVerified: false,
                    notes: $"Resolved through provider-assisted fixture matching. Matches={bestMatchCount}.",
                    touchLastUsed: true,
                    cancellationToken);

                CacheResolved(leagueApiId, bestMatch.Sport.Key);

                _logger.LogInformation(
                    "Resolved The Odds sport key via provider-assisted discovery for league {LeagueApiId}/{Season}: {SportKey} (matches {MatchCount})",
                    leagueApiId,
                    season,
                    bestMatch.Sport.Key,
                    bestMatchCount);

                return new TheOddsSportKeyResolutionResult
                {
                    SportKey = bestMatch.Sport.Key,
                    RequestsUsed = requestsUsed,
                    Source = "provider_discovery",
                    Confidence = discoveryConfidence
                };
            }
        }

        await UpsertMappingAsync(
            league,
            sportKey: null,
            resolutionSource: "unresolved",
            confidence: 0,
            isVerified: false,
            notes: "No reliable The Odds sport key candidate could be resolved.",
            touchLastUsed: false,
            cancellationToken);

        CacheUnresolved(leagueApiId);

        return new TheOddsSportKeyResolutionResult
        {
            RequestsUsed = requestsUsed,
            Source = "unresolved"
        };
    }

    public async Task<IReadOnlyList<TheOddsSportKeyCandidateDto>> SuggestCandidatesAsync(
        long leagueApiId,
        int season,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 20);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .Include(x => x.Country)
            .Where(x => x.ApiLeagueId == leagueApiId && x.Season == season)
            .Select(x => new LeagueLookupContext
            {
                LeagueApiId = x.ApiLeagueId,
                Season = x.Season,
                LeagueName = x.Name,
                CountryName = x.Country.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (league is null)
            return Array.Empty<TheOddsSportKeyCandidateDto>();

        var sports = (await GetActiveSoccerSportsAsync(cancellationToken)).Sports;
        var candidates = BuildCandidates(league, sports)
            .Take(limit)
            .Select(x => new TheOddsSportKeyCandidateDto
            {
                SportKey = x.Sport.Key,
                Title = x.Sport.Title,
                Description = x.Sport.Description,
                Score = x.Score
            })
            .ToList();

        return candidates;
    }

    private async Task<(IReadOnlyList<TheOddsApiSport> Sports, int RequestsUsed)> GetActiveSoccerSportsAsync(
        CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<TheOddsApiSport>>(SportsCacheKey, out var cachedSports) &&
            cachedSports is not null)
        {
            return (cachedSports, 0);
        }

        var sports = await _apiService.GetActiveSoccerSportsAsync(cancellationToken);
        _memoryCache.Set(SportsCacheKey, sports, SportsCacheDuration);

        return (sports, 1);
    }

    private async Task UpsertMappingAsync(
        LeagueLookupContext league,
        string? sportKey,
        string resolutionSource,
        int confidence,
        bool isVerified,
        string? notes,
        bool touchLastUsed,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var mapping = await _dbContext.TheOddsLeagueMappings
            .FirstOrDefaultAsync(x => x.ApiFootballLeagueId == league.LeagueApiId, cancellationToken);

        if (mapping is null)
        {
            mapping = new TheOddsLeagueMapping
            {
                ApiFootballLeagueId = league.LeagueApiId,
                CreatedAtUtc = nowUtc
            };
            _dbContext.TheOddsLeagueMappings.Add(mapping);
        }

        mapping.LeagueName = league.LeagueName;
        mapping.CountryName = league.CountryName;
        mapping.TheOddsSportKey = sportKey;
        mapping.ResolutionSource = resolutionSource;
        mapping.Confidence = confidence;
        mapping.IsVerified = isVerified;
        mapping.Notes = notes;
        mapping.LastResolvedAtUtc = nowUtc;
        mapping.LastUsedAtUtc = touchLastUsed ? nowUtc : mapping.LastUsedAtUtc;
        mapping.UpdatedAtUtc = nowUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? TryResolveFromKnownAliases(LeagueLookupContext league)
    {
        var normalizedCountry = NormalizeForCompare(league.CountryName);
        var normalizedLeagueName = NormalizeForCompare(league.LeagueName);

        if (KnownLeagueAliases.TryGetValue($"{normalizedCountry}|{normalizedLeagueName}", out var knownSportKey))
        {
            return knownSportKey;
        }

        return null;
    }

    private static List<SportCandidateScore> BuildCandidates(
        LeagueLookupContext league,
        IReadOnlyCollection<TheOddsApiSport> sports)
    {
        return sports
            .Select(x => new SportCandidateScore
            {
                Sport = x,
                Score = ScoreCandidate(league, x)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Sport.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static int ScoreCandidate(
        LeagueLookupContext league,
        TheOddsApiSport sport)
    {
        var normalizedLeagueName = NormalizeForCompare(league.LeagueName);
        var normalizedCountry = NormalizeForCompare(league.CountryName);
        var normalizedTitle = NormalizeForCompare(sport.Title);
        var normalizedDescription = NormalizeForCompare(sport.Description);
        var normalizedKey = NormalizeForCompare(sport.Key);
        var candidateText = $"{normalizedTitle} {normalizedDescription} {normalizedKey}";
        var candidateTokens = Tokenize(candidateText);

        var score = 0;

        if (!string.IsNullOrWhiteSpace(normalizedLeagueName))
        {
            if (normalizedTitle == normalizedLeagueName || normalizedDescription == normalizedLeagueName)
                score += 100;

            if (candidateText.Contains(normalizedLeagueName, StringComparison.Ordinal))
                score += 60;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCountry))
        {
            if (candidateText.Contains(normalizedCountry, StringComparison.Ordinal))
                score += 30;

            foreach (var alias in GetCountryAliases(normalizedCountry))
            {
                if (candidateText.Contains(alias, StringComparison.Ordinal))
                {
                    score += 25;
                    break;
                }
            }
        }

        foreach (var token in Tokenize(league.LeagueName))
        {
            if (candidateTokens.Contains(token))
                score += 14;
        }

        foreach (var token in Tokenize(league.CountryName))
        {
            if (candidateTokens.Contains(token))
                score += 8;
        }

        return score;
    }

    private static bool MatchesAnyFixture(
        TheOddsApiOddsItem providerEvent,
        IReadOnlyCollection<TheOddsFixtureLookupContext> fixtures,
        TimeSpan tolerance)
    {
        var normalizedHomeTeam = NormalizeForCompare(providerEvent.HomeTeam);
        var normalizedAwayTeam = NormalizeForCompare(providerEvent.AwayTeam);

        return fixtures.Any(x =>
            NormalizeForCompare(x.HomeTeamName) == normalizedHomeTeam &&
            NormalizeForCompare(x.AwayTeamName) == normalizedAwayTeam &&
            Math.Abs((x.KickoffAtUtc - providerEvent.CommenceTime).TotalMinutes) <= tolerance.TotalMinutes);
    }

    private static IEnumerable<string> GetCountryAliases(string normalizedCountry)
    {
        if (CountryAliases.TryGetValue(normalizedCountry, out var aliases))
            return aliases.Select(NormalizeForCompare);

        return [normalizedCountry];
    }

    private static HashSet<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<string>(StringComparer.Ordinal);

        return NormalizeTokens(value)
            .Where(x => x.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> NormalizeTokens(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeForCompare(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Concat(NormalizeTokens(value));
    }

    private void CacheResolved(long leagueApiId, string sportKey) =>
        _memoryCache.Set(BuildCacheKey(leagueApiId), sportKey, ResolvedCacheDuration);

    private void CacheUnresolved(long leagueApiId) =>
        _memoryCache.Set(BuildCacheKey(leagueApiId), UnresolvedCacheValue, UnresolvedCacheDuration);

    private static string BuildCacheKey(long leagueApiId) =>
        $"theodds:sportkey:{leagueApiId}";

    private sealed class LeagueLookupContext
    {
        public long LeagueApiId { get; set; }
        public int Season { get; set; }
        public string LeagueName { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
    }

    private sealed class SportCandidateScore
    {
        public required TheOddsApiSport Sport { get; init; }
        public int Score { get; init; }
    }
}

public class TheOddsSportKeyResolutionResult
{
    public string? SportKey { get; set; }
    public int RequestsUsed { get; set; }
    public string Source { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public bool IsVerified { get; set; }
}

public class TheOddsFixtureLookupContext
{
    public DateTime KickoffAtUtc { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
}

public class TheOddsSportKeyCandidateDto
{
    public string SportKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Score { get; set; }
}
