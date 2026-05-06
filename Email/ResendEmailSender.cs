using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CadastraAI.API.Email;

public class ResendEmailSender(
    HttpClient http,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Resend.ApiKey))
        {
            logger.LogWarning("Resend API key is not configured; skipping email to {To}", message.To);
            return new EmailSendResult(false, "Resend API key não configurada.");
        }

        var to = string.IsNullOrWhiteSpace(message.ToName)
            ? message.To
            : $"{message.ToName} <{message.To}>";

        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromAddress
            : $"{_options.FromName} <{_options.FromAddress}>";

        var payload = new
        {
            from,
            to = new[] { to },
            subject = message.Subject,
            html = message.HtmlBody,
            text = message.TextBody,
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/emails")
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);

            using var response = await http.SendAsync(request, ct);
            var bodyText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = ExtractResendError(bodyText) ?? bodyText;

                // Common Resend testing-mode error: trying to send from onboarding@resend.dev
                // to anything other than the account owner's email.
                if (errorMessage.Contains("testing emails", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("verify a domain", StringComparison.OrdinalIgnoreCase) ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    errorMessage =
                        "Resend bloqueou a entrega: enquanto você não verificar um domínio em resend.com/domains, " +
                        "o remetente onboarding@resend.dev só envia para o e-mail dono da conta Resend. " +
                        "Verifique seu domínio e troque Email:FromAddress no appsettings.";
                }

                logger.LogWarning(
                    "Resend rejected the message to {To}: {Status} {Body}",
                    message.To,
                    (int)response.StatusCode,
                    bodyText);

                return new EmailSendResult(false, errorMessage, (int)response.StatusCode);
            }

            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.TryGetProperty("id", out var idEl))
            {
                logger.LogInformation("Resend accepted email {EmailId} to {To}", idEl.GetString(), message.To);
            }
            return new EmailSendResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email via Resend to {To}", message.To);
            return new EmailSendResult(false, $"Falha ao chamar a Resend: {ex.Message}");
        }
    }

    private static string? ExtractResendError(string bodyText)
    {
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();
        }
        catch
        {
            // not JSON — return null so caller falls back to raw body
        }
        return null;
    }
}
