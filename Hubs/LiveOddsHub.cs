using Microsoft.AspNetCore.SignalR;

namespace SmartBets.Hubs;

public class LiveOddsHub : Hub
{
    public const string Route = "/hubs/live-odds";
    public const string LiveOddsUpdatedEventName = "LiveOddsUpdated";

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

    public static string GetFixtureGroup(long apiFixtureId)
    {
        return $"fixture:{apiFixtureId}";
    }

    public static string GetLeagueGroup(long leagueId)
    {
        return $"league:{leagueId}";
    }
}
