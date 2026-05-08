using System.Text.Json;
using CadastraAI.API.Auth;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Authorize]
public class AuditLogController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/empresas/{empresaId:guid}/audit-log")]
    public async Task<ActionResult<AuditLogPageDto>> List(
        Guid empresaId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? actions,
        [FromQuery] Guid? userId,
        [FromQuery] string? entityType,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var caller = User.UserId();
        if (caller is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, caller.Value, ct) is null) return Forbid();

        page = Math.Max(0, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var q = db.AuditLogs.AsNoTracking().Where(a => a.EmpresaId == empresaId);

        if (from is not null) q = q.Where(a => a.At >= from);
        if (to is not null) q = q.Where(a => a.At < to);
        if (userId is not null) q = q.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(actions))
        {
            var list = actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            q = q.Where(a => list.Contains(a.Action));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(a => a.At)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.EmpresaId, a.UserId, a.UserName, a.UserEmail, a.Action,
                a.EntityType, a.EntityId, a.EntityLabel, a.Meta, a.Ip, a.At,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AuditLogEntryDto(
            r.Id, r.EmpresaId, r.UserId, r.UserName, r.UserEmail,
            r.Action, r.EntityType, r.EntityId, r.EntityLabel,
            ExtractChangedFields(r.Meta),
            r.Ip, r.At)).ToList();

        return Ok(new AuditLogPageDto(items, total, page, pageSize));
    }

    private static IReadOnlyList<string>? ExtractChangedFields(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            if (!doc.RootElement.TryGetProperty("changed", out var changed)) return null;
            if (changed.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string>(changed.GetArrayLength());
            foreach (var item in changed.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString()!);
            }
            return list;
        }
        catch
        {
            return null;
        }
    }
}
