namespace SmartBets.Entities;

public class SupportedLeague
{
    public long Id { get; set; }
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}