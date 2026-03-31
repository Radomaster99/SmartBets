namespace SmartBets.Dtos;

public class LiveFixtureStatusSyncResultDto
{
    public bool ScopedToActiveSupportedLeagues { get; set; }
    public int TargetLeagueCount { get; set; }
    public int LiveFixturesReceived { get; set; }
    public int FixturesProcessed { get; set; }
    public int FixturesInserted { get; set; }
    public int FixturesUpdated { get; set; }
    public int FixturesUnchanged { get; set; }
    public int FixturesSkippedMissingLeague { get; set; }
    public int FixturesSkippedMissingTeams { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}
