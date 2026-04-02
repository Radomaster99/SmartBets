namespace SmartBets.Services;

public static class CoreAutomationJobNames
{
    public const string CatalogRefresh = "catalog_refresh";
    public const string TeamsRolling = "teams_rolling";
    public const string FixturesRolling = "fixtures_rolling";
    public const string OddsPreMatch = "odds_pre_match";
    public const string OddsLive = "odds_live";
    public const string Repair = "repair";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CatalogRefresh,
        TeamsRolling,
        FixturesRolling,
        OddsPreMatch,
        OddsLive,
        Repair
    };
}

public class CoreAutomationQuotaManager
{
    private readonly object _sync = new();
    private DateOnly _currentDayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly Dictionary<string, CoreAutomationJobRuntimeSnapshot> _jobs =
        CoreAutomationJobNames.All.ToDictionary(
            x => x,
            x => new CoreAutomationJobRuntimeSnapshot { Job = x },
            StringComparer.Ordinal);

    public int GetAllowedRequests(
        string job,
        int desiredRequests,
        CoreDataAutomationOptions options,
        ApiFootballQuotaSnapshot apiQuotaSnapshot)
    {
        if (desiredRequests <= 0)
            return 0;

        lock (_sync)
        {
            EnsureCurrentDay();

            if (!_jobs.TryGetValue(job, out var state))
                return 0;

            var jobRemaining = Math.Max(0, GetDailyBudget(job, options) - state.UsedToday);
            var automationRemaining = Math.Max(0, options.GetAutomationDailyBudget() - _jobs.Values.Sum(x => x.UsedToday));

            var providerRemainingGuarded = int.MaxValue;
            if (apiQuotaSnapshot.RequestsDailyRemaining.HasValue)
            {
                providerRemainingGuarded = Math.Max(
                    0,
                    apiQuotaSnapshot.RequestsDailyRemaining.Value - options.GetProviderDailySafetyBuffer());
            }

            return new[]
            {
                desiredRequests,
                jobRemaining,
                automationRemaining,
                providerRemainingGuarded
            }.Min();
        }
    }

    public void MarkStarted(string job, int desiredRequests, string? note = null)
    {
        lock (_sync)
        {
            EnsureCurrentDay();
            if (!_jobs.TryGetValue(job, out var state))
                return;

            state.LastStartedAtUtc = DateTime.UtcNow;
            state.LastStatus = "Started";
            state.LastReason = note;
            state.LastDesiredRequests = desiredRequests;
        }
    }

    public void MarkCompleted(string job, int actualRequests, int processedItems, string? note = null)
    {
        lock (_sync)
        {
            EnsureCurrentDay();
            if (!_jobs.TryGetValue(job, out var state))
                return;

            state.UsedToday += Math.Max(0, actualRequests);
            state.LastCompletedAtUtc = DateTime.UtcNow;
            state.LastStatus = "Completed";
            state.LastReason = note;
            state.LastActualRequests = Math.Max(0, actualRequests);
            state.LastProcessedItems = Math.Max(0, processedItems);
        }
    }

    public void MarkSkipped(string job, string reason, int desiredRequests = 0)
    {
        lock (_sync)
        {
            EnsureCurrentDay();
            if (!_jobs.TryGetValue(job, out var state))
                return;

            state.LastSkippedAtUtc = DateTime.UtcNow;
            state.LastStatus = "Skipped";
            state.LastReason = reason;
            state.LastDesiredRequests = Math.Max(0, desiredRequests);
            state.LastActualRequests = 0;
        }
    }

    public CoreAutomationQuotaSnapshot GetSnapshot(CoreDataAutomationOptions options)
    {
        lock (_sync)
        {
            EnsureCurrentDay();

            var totalUsed = _jobs.Values.Sum(x => x.UsedToday);
            return new CoreAutomationQuotaSnapshot
            {
                DayUtc = _currentDayUtc,
                DailyBudget = options.GetAutomationDailyBudget(),
                UsedToday = totalUsed,
                RemainingToday = Math.Max(0, options.GetAutomationDailyBudget() - totalUsed),
                Jobs = CoreAutomationJobNames.All
                    .Select(job =>
                    {
                        var state = _jobs[job];
                        var jobBudget = GetDailyBudget(job, options);
                        return new CoreAutomationJobRuntimeSnapshot
                        {
                            Job = state.Job,
                            DailyBudget = jobBudget,
                            UsedToday = state.UsedToday,
                            RemainingToday = Math.Max(0, jobBudget - state.UsedToday),
                            LastStartedAtUtc = state.LastStartedAtUtc,
                            LastCompletedAtUtc = state.LastCompletedAtUtc,
                            LastSkippedAtUtc = state.LastSkippedAtUtc,
                            LastStatus = state.LastStatus,
                            LastReason = state.LastReason,
                            LastDesiredRequests = state.LastDesiredRequests,
                            LastActualRequests = state.LastActualRequests,
                            LastProcessedItems = state.LastProcessedItems
                        };
                    })
                    .ToList()
            };
        }
    }

    private void EnsureCurrentDay()
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (todayUtc == _currentDayUtc)
            return;

        _currentDayUtc = todayUtc;

        foreach (var state in _jobs.Values)
        {
            state.UsedToday = 0;
            state.DailyBudget = 0;
            state.RemainingToday = 0;
        }
    }

    private static int GetDailyBudget(string job, CoreDataAutomationOptions options)
    {
        return job switch
        {
            CoreAutomationJobNames.CatalogRefresh => options.GetCatalogRefreshDailyBudget(),
            CoreAutomationJobNames.TeamsRolling => options.GetTeamsRollingDailyBudget(),
            CoreAutomationJobNames.FixturesRolling => options.GetFixturesRollingDailyBudget(),
            CoreAutomationJobNames.OddsPreMatch => options.GetOddsPreMatchDailyBudget(),
            CoreAutomationJobNames.OddsLive => options.GetOddsLiveDailyBudget(),
            CoreAutomationJobNames.Repair => options.GetRepairDailyBudget(),
            _ => 0
        };
    }
}

public class CoreAutomationQuotaSnapshot
{
    public DateOnly DayUtc { get; init; }
    public int DailyBudget { get; init; }
    public int UsedToday { get; init; }
    public int RemainingToday { get; init; }
    public IReadOnlyList<CoreAutomationJobRuntimeSnapshot> Jobs { get; init; } = Array.Empty<CoreAutomationJobRuntimeSnapshot>();
}

public class CoreAutomationJobRuntimeSnapshot
{
    public string Job { get; set; } = string.Empty;
    public int DailyBudget { get; set; }
    public int UsedToday { get; set; }
    public int RemainingToday { get; set; }
    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastCompletedAtUtc { get; set; }
    public DateTime? LastSkippedAtUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastReason { get; set; }
    public int? LastDesiredRequests { get; set; }
    public int? LastActualRequests { get; set; }
    public int? LastProcessedItems { get; set; }
}
