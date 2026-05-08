using System.Diagnostics;
using System.Text;
using CadastraAI.API.Auth;

namespace CadastraAI.API.Admin;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    IRequestLogStore store,
    ILogger<RequestLoggingMiddleware> logger)
{
    private const int MaxBodyBytes = 8 * 1024;

    private static readonly string[] SkipPrefixes =
    {
        "/uploads",
        "/openapi",
        "/swagger",
    };

    private static readonly string[] SensitivePathFragments =
    {
        "/api/auth/login",
        "/api/auth/register",
        "/accept-password",
        "/accept-google",
    };

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path == "/favicon.ico" || SkipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        string? body = null;

        var isWrite = method is "POST" or "PUT" or "PATCH" or "DELETE";
        var isSensitive = SensitivePathFragments.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (isWrite
            && !isSensitive
            && context.Request.ContentLength is > 0 and <= MaxBodyBytes
            && (context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            try
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(
                    context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Falha ao bufferizar body para log");
                body = null;
            }
        }

        string? errorMessage = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();

            string? userId = null;
            string? userEmail = null;
            try
            {
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    userId = context.User.UserId()?.ToString();
                    userEmail = context.User.Email();
                }
            }
            catch { /* ignore */ }

            var ua = context.Request.Headers.UserAgent.ToString();

            store.Add(new RequestLogEntry(
                At: DateTime.UtcNow,
                Method: method,
                Path: path,
                Query: context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                Status: context.Response.StatusCode,
                DurationMs: sw.ElapsedMilliseconds,
                UserId: userId,
                UserEmail: userEmail,
                Ip: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: string.IsNullOrWhiteSpace(ua) ? null : ua,
                RequestBody: body,
                ErrorMessage: errorMessage));
        }
    }
}
