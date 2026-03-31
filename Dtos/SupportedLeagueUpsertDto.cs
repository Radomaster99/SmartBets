namespace SmartBets.Dtos;

public class SupportedLeagueUpsertDto
{
    public long LeagueApiId { get; set; }
    public int Season { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
}

public class SupportedLeagueUpdateDto
{
    public bool? IsActive { get; set; }
    public int? Priority { get; set; }
}
