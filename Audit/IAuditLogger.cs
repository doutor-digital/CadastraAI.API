using System.Security.Claims;
using CadastraAI.API.Models;

namespace CadastraAI.API.Audit;

/// <summary>
/// Escreve eventos na tabela audit_logs. Resolve usuário/IP/UA do HttpContext
/// automaticamente, então os controllers só precisam chamar LogAsync com o domínio.
/// Erros do logger são engolidos silenciosamente (com ILogger.Warning) para não derrubar
/// uma operação de domínio por causa de telemetria — auditoria é "best effort".
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        Guid empresaId,
        string action,
        string entityType,
        Guid? entityId,
        string? entityLabel = null,
        IReadOnlyCollection<string>? changedFields = null,
        IReadOnlyDictionary<string, object?>? extraMeta = null,
        CancellationToken ct = default);
}
