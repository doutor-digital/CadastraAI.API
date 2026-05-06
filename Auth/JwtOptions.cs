namespace CadastraAI.API.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "cadastraai";
    public string Audience { get; set; } = "cadastraai";
    public string Key { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 168;
}
