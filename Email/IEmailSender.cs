namespace CadastraAI.API.Email;

public record EmailSendResult(bool Delivered, string? ErrorMessage = null, int? StatusCode = null);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}
