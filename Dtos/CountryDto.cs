namespace SmartBets.Dtos;

public class CountryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? FlagUrl { get; set; }
}