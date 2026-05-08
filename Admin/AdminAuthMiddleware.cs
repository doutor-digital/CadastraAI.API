using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace CadastraAI.API.Admin;

/// <summary>
/// Basic Auth gate for /api/admin/*. Runs before the controller pipeline so the admin
/// endpoints don't depend on JWT authentication.
/// </summary>
public sealed class AdminAuthMiddleware(RequestDelegate next, IOptionsMonitor<AdminOptions> options)
{
    private const string Realm = "cadastra.ai admin";

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var opts = options.CurrentValue;
        if (!opts.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!TryGetBasicCredentials(context, out var user, out var pass)
            || !FixedTimeEquals(user, opts.Username)
            || !FixedTimeEquals(pass, opts.Password))
        {
            context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Realm}\", charset=\"UTF-8\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Acesso restrito.");
            return;
        }

        await next(context);
    }

    private static bool TryGetBasicCredentials(HttpContext context, out string user, out string pass)
    {
        user = pass = string.Empty;
        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var sep = raw.IndexOf(':');
            if (sep <= 0) return false;
            user = raw[..sep];
            pass = raw[(sep + 1)..];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
