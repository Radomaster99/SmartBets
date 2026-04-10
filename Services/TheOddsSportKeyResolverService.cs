using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartBets.Data;
using SmartBets.Models.TheOddsApi;

namespace SmartBets.Services;

public class TheOddsSportKeyResolverService
{
    private const string UnresolvedCacheValue = "__UNRESOLVED__";
    private const string SportsCacheKey = "theodds:active-soccer-sports";

    private static readonly TimeSpan SportsCacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan ResolvedCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan UnresolvedCacheDuration = TimeSpan.FromMinutes(30);

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
        if (!string.IsNullOrWhiteSpace(configuredSportKey))
        {
            return new TheOddsSportKeyResolutionResult
            {
                SportKey = configuredSportKey.Trim(),
                Source = "configured_override"
            };
        }

        var cacheKey = BuildCacheKey(leagueApiId, season);
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
        {
            _memoryCache.Set(cacheKey, UnresolvedCacheValue, UnresolvedCacheDuration);
            return new TheOddsSportKeyResolutionResult();
        }

        var sportsLoad = await GetActiveSoccerSportsAsync(cancellationToken);
        var sports = sportsLoad.Sports;
        var requestsUsed = sportsLoad.RequestsUsed;

        var aliasMatch = TryResolveFromKnownAliases(league, sports);
        if (!string.IsNullOrWhiteSpace(aliasMatch))
        {
            _memoryCache.Set(cacheKey, aliasMatch, ResolvedCacheDuration);

            return new TheOddsSportKeyResolutionResult
            {
                SportKey = aliasMatch,
                RequestsUsed = requestsUsed,
                Source = "known_alias"
            };
        }

        var candidates = sports
            .Select(x => new SportCandidateScore
            {
                Sport = x,
                Score = ScoreCandidate(league, x)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Sport.Key, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count > 0)
        {
            var best = candidates[0];
            var secondScore = candidates.Count > 1 ? candidates[1].Score : int.MinValue;
            if (best.Score >= 100 && best.Score - secondScore >= 15)
            {
                _memoryCache.Set(cacheKey, best.Sport.Key, ResolvedCacheDuration);

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
                    Source = "heuristic"
                };
            }
        }

        if (fixtures is not null && fixtures.Count > 0 && candidates.Count > 0)
        {
            var discoveryCandidates = candidates
                .Take(3)
                .ToList();

            var commenceTimeFromUtc = fixtures.Min(x => x.KickoffAtUtc).Subtract(matchTolerance);
            var commenceTimeToUtc = fixtures.Max(x => x.KickoffAtUtc).Add(matchTolerance);

            var bestMatch = default(SportCandidateScore);
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
                _memoryCache.Set(cacheKey, bestMatch.Sport.Key, ResolvedCacheDuration);

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
                    Source = "provider_discovery"
                };
            }
        }

        _memoryCache.Set(cacheKey, UnresolvedCacheValue, UnresolvedCacheDuration);

        return new TheOddsSportKeyResolutionResult
        {
            RequestsUsed = requestsUsed,
            Source = "unresolved"
        };
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

    private static string? TryResolveFromKnownAliases(
        LeagueLookupContext league,
        IReadOnlyCollection<TheOddsApiSport> sports)
    {
        var normalizedCountry = NormalizeForCompare(league.CountryName);
        var normalizedLeagueName = NormalizeForCompare(league.LeagueName);

        if (KnownLeagueAliases.TryGetValue($"{normalizedCountry}|{normalizedLeagueName}", out var knownSportKey) &&
            sports.Any(x => string.Equals(x.Key, knownSportKey, StringComparison.OrdinalIgnoreCase)))
        {
            return knownSportKey;
        }

        return null;
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

    private static string BuildCacheKey(long leagueApiId, int season) =>
        $"theodds:sportkey:{leagueApiId}:{season}";

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
}

public class TheOddsFixtureLookupContext
{
    public DateTime KickoffAtUtc { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
}
