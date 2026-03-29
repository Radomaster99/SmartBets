namespace SmartBets.Dtos;

public class OddDto
{
    public string Bookmaker { get; set; } = string.Empty;
    public decimal? HomeOdd { get; set; }
    public decimal? DrawOdd { get; set; }
    public decimal? AwayOdd { get; set; }
}