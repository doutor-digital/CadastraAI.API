using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace CadastraAI.API.Auth;

public record GoogleIdentity(string Subject, string Email, string Name, string? Picture);

public interface IGoogleTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct);
}

public class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException(
                "Authentication:Google:ClientId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidOperationException("Google ID token is required.");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _options.ClientId },
        };

        // GoogleJsonWebSignature.ValidateAsync does not take a CancellationToken;
        // ct is preserved for future use / interface stability.
        _ = ct;
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

        if (string.IsNullOrEmpty(payload.Subject))
        {
            throw new InvalidOperationException("Google token did not include a subject.");
        }
        if (string.IsNullOrEmpty(payload.Email) || !payload.EmailVerified)
        {
            throw new InvalidOperationException("Google email is missing or not verified.");
        }

        return new GoogleIdentity(
            payload.Subject,
            payload.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
            payload.Picture);
    }
}
