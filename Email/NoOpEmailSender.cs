using Microsoft.Extensions.Logging;

namespace CadastraAI.API.Email;

public class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Email provider disabled — would send to {To}: {Subject}",
            message.To,
            message.Subject);
        return Task.FromResult(new EmailSendResult(false, "Email provider desativado (Email:Provider != Resend)."));
    }
}
