using Microsoft.AspNetCore.SignalR;

namespace SmartBets.Hubs;

public class LiveOddsHub : Hub
{
    public const string Route = "/hubs/live-odds";
    public const string LiveOddsUpdatedEventName = "LiveOddsUpdated";
    public const string LiveOddsSummaryUpdatedEventName = "LiveOddsSummaryUpdated";
    public const string LiveFeedGroup = "live-feed";

    public Task JoinFixture(long apiFixtureId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GetFixtureGroup(apiFixtureId));
    }

    public Task LeaveFixture(long apiFixtureId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetFixtureGroup(apiFixtureId));
    }

    public Task JoinLeague(long leagueId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GetLeagueGroup(leagueId));
    }

    public Task LeaveLeague(long leagueId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetLeagueGroup(leagueId));
    }

    public Task JoinFixtures(IReadOnlyList<long> apiFixtureIds)
    {
        return ManageFixtureGroupsAsync(apiFixtureIds, join: true);
    }

    public Task LeaveFixtures(IReadOnlyList<long> apiFixtureIds)
    {
        return ManageFixtureGroupsAsync(apiFixtureIds, join: false);
    }

    public Task JoinLiveFeed()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, LiveFeedGroup);
    }

    public Task LeaveLiveFeed()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveFeedGroup);
    }

    public static string GetFixtureGroup(long apiFixtureId)
    {
        return $"fixture:{apiFixtureId}";
    }

    public static string GetLeagueGroup(long leagueId)
    {
        return $"league:{leagueId}";
    }

    private Task ManageFixtureGroupsAsync(IReadOnlyList<long> apiFixtureIds, bool join)
    {
        if (apiFixtureIds.Count == 0)
            return Task.CompletedTask;

        var distinctIds = apiFixtureIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var operations = distinctIds.Select(apiFixtureId =>
            join
                ? Groups.AddToGroupAsync(Context.ConnectionId, GetFixtureGroup(apiFixtureId))
                : Groups.RemoveFromGroupAsync(Context.ConnectionId, GetFixtureGroup(apiFixtureId)));

        return Task.WhenAll(operations);
    }
}
