using System.Text.Json;
using CadastraAI.API.Auth;
using CadastraAI.API.Data;
using CadastraAI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Audit;

public class AuditLogger(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    public async Task LogAsync(
        Guid empresaId,
        string action,
        string entityType,
        Guid? entityId,
        string? entityLabel = null,
        IReadOnlyCollection<string>? changedFields = null,
        IReadOnlyDictionary<string, object?>? extraMeta = null,
        CancellationToken ct = default)
    {
        try
        {
            var http = httpContextAccessor.HttpContext;
            var userId = http?.User.UserId();
            var userEmail = http?.User.Email();

            string? userName = null;
            if (userId.HasValue)
            {
                userName = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId.Value)
                    .Select(u => u.Name)
                    .FirstOrDefaultAsync(ct);
            }

            string? metaJson = null;
            if (changedFields is { Count: > 0 } || extraMeta is { Count: > 0 })
            {
                var meta = new Dictionary<string, object?>();
                if (changedFields is { Count: > 0 }) meta["changed"] = changedFields.ToArray();
                if (extraMeta is not null)
                {
                    foreach (var kv in extraMeta) meta[kv.Key] = kv.Value;
                }
                metaJson = JsonSerializer.Serialize(meta);
            }

            var ip = http?.Connection.RemoteIpAddress?.ToString();
            var ua = http?.Request.Headers.UserAgent.ToString();
            if (ua is { Length: > 500 }) ua = ua[..500];

            db.AuditLogs.Add(new AuditLog
            {
                EmpresaId = empresaId,
                UserId = userId,
                UserName = userName,
                UserEmail = userEmail,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityLabel = entityLabel,
                Meta = metaJson,
                Ip = ip,
                UserAgent = ua,
                At = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log write failed for action {Action} on {EntityType}", action, entityType);
        }
    }
}
