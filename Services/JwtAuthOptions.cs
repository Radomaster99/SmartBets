namespace SmartBets.Services;

public class JwtAuthOptions
{
    public string Issuer { get; set; } = "SmartBets";
    public string Audience { get; set; } = "SmartBets.Frontend";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 480;

    public int GetAccessTokenMinutes()
    {
        return Math.Clamp(AccessTokenMinutes, 5, 7 * 24 * 60);
    }
}
