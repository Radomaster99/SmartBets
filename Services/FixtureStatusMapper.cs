using SmartBets.Enums;

namespace SmartBets.Services;

public static class FixtureStatusMapper
{
    private static readonly string[] UpcomingStatuses = ["TBD", "NS"];
    private static readonly string[] LiveStatuses = ["1H", "HT", "2H", "ET", "BT", "P", "INT", "SUSP", "LIVE"];
    private static readonly string[] FinishedStatuses = ["FT", "AET", "PEN"];
    private static readonly string[] PostponedStatuses = ["PST"];
    private static readonly string[] CancelledStatuses = ["CANC", "ABD", "AWD", "WO"];

    private static readonly HashSet<string> KnownStatuses = new(
        UpcomingStatuses
            .Concat(LiveStatuses)
            .Concat(FinishedStatuses)
            .Concat(PostponedStatuses)
            .Concat(CancelledStatuses),
        StringComparer.OrdinalIgnoreCase);

    public static string? NormalizeShort(string? statusShort)
    {
        if (string.IsNullOrWhiteSpace(statusShort))
            return null;

        return statusShort.Trim().ToUpperInvariant();
    }

    public static FixtureStateBucket GetStateBucket(string? statusShort)
    {
        var normalizedStatus = NormalizeShort(statusShort);

        if (normalizedStatus is null)
            return FixtureStateBucket.Other;

        if (UpcomingStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            return FixtureStateBucket.Upcoming;

        if (LiveStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            return FixtureStateBucket.Live;

        if (FinishedStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            return FixtureStateBucket.Finished;

        if (PostponedStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            return FixtureStateBucket.Postponed;

        if (CancelledStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            return FixtureStateBucket.Cancelled;

        return FixtureStateBucket.Other;
    }

    public static IReadOnlyCollection<string> GetStatusesForBucket(FixtureStateBucket bucket)
    {
        return bucket switch
        {
            FixtureStateBucket.Upcoming => UpcomingStatuses,
            FixtureStateBucket.Live => LiveStatuses,
            FixtureStateBucket.Finished => FinishedStatuses,
            FixtureStateBucket.Postponed => PostponedStatuses,
            FixtureStateBucket.Cancelled => CancelledStatuses,
            _ => Array.Empty<string>()
        };
    }

    public static bool IsKnownStatus(string? statusShort)
    {
        var normalizedStatus = NormalizeShort(statusShort);
        return normalizedStatus is not null && KnownStatuses.Contains(normalizedStatus);
    }

    public static IReadOnlyCollection<string> GetKnownStatuses()
    {
        return KnownStatuses.ToArray();
    }
}
