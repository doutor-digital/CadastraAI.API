namespace CadastraAI.API.Email;

public class EmailOptions
{
    /// <summary>"Resend" or "None" (no-op for local dev without an API key).</summary>
    public string Provider { get; set; } = "None";
    public string FromAddress { get; set; } = "onboarding@resend.dev";
    public string FromName { get; set; } = "cadastra.ai";
    public ResendOptions Resend { get; set; } = new();
}

public class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.resend.com";
}
