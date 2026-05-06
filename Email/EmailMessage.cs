namespace CadastraAI.API.Email;

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    string? ToName = null);
