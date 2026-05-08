using System.Text.Json;
using CadastraAI.API.Audit;
using CadastraAI.API.Auth;
using CadastraAI.API.Cache;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Kommo;
using CadastraAI.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
public class KommoController(
    AppDbContext db,
    IDashboardCache cache,
    IAuditLogger audit,
    IKommoClient kommo,
    IKommoTokenProtector protector,
    ILogger<KommoController> logger) : ControllerBase
{
    // ===== Configuração =====

    [Authorize]
    [HttpGet("api/empresas/{empresaId:guid}/kommo/config")]
    public async Task<ActionResult<KommoConfigDto>> GetConfig(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var cfg = await db.KommoIntegrations.AsNoTracking().FirstOrDefaultAsync(k => k.EmpresaId == empresaId, ct);
        if (cfg is null) return Ok((KommoConfigDto?)null);
        return Ok(new KommoConfigDto(
            cfg.Subdomain,
            !string.IsNullOrEmpty(cfg.AccessTokenEncrypted),
            cfg.TokenSuffix,
            !string.IsNullOrEmpty(cfg.WebhookSecret),
            cfg.LastSyncAt));
    }

    [Authorize]
    [HttpPut("api/empresas/{empresaId:guid}/kommo/config")]
    public async Task<ActionResult<KommoConfigDto>> SaveConfig(
        Guid empresaId, [FromBody] SaveKommoConfigRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Subdomain) || string.IsNullOrWhiteSpace(req.AccessToken))
            return BadRequest(new { message = "Subdomínio e access token são obrigatórios." });

        // Valida token antes de persistir.
        try
        {
            await kommo.PingAsync(req.Subdomain, req.AccessToken, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Falha ao autenticar na Kommo: {ex.Message}" });
        }

        var cfg = await db.KommoIntegrations.FirstOrDefaultAsync(k => k.EmpresaId == empresaId, ct);
        var now = DateTime.UtcNow;
        var suffix = req.AccessToken.Length >= 4 ? req.AccessToken[^4..] : req.AccessToken;

        if (cfg is null)
        {
            cfg = new KommoIntegration
            {
                EmpresaId = empresaId,
                Subdomain = req.Subdomain.Trim(),
                AccessTokenEncrypted = protector.Encrypt(req.AccessToken),
                TokenSuffix = suffix,
                WebhookSecret = string.IsNullOrWhiteSpace(req.WebhookSecret) ? null : req.WebhookSecret.Trim(),
                CreatedAt = now,
                CreatedByUserId = userId,
            };
            db.KommoIntegrations.Add(cfg);
        }
        else
        {
            cfg.Subdomain = req.Subdomain.Trim();
            cfg.AccessTokenEncrypted = protector.Encrypt(req.AccessToken);
            cfg.TokenSuffix = suffix;
            cfg.WebhookSecret = string.IsNullOrWhiteSpace(req.WebhookSecret) ? null : req.WebhookSecret.Trim();
            cfg.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(empresaId, "kommo.config.save", "KommoIntegration", cfg.Id, cfg.Subdomain, ct: ct);

        return Ok(new KommoConfigDto(cfg.Subdomain, true, cfg.TokenSuffix, !string.IsNullOrEmpty(cfg.WebhookSecret), cfg.LastSyncAt));
    }

    [Authorize]
    [HttpDelete("api/empresas/{empresaId:guid}/kommo/config")]
    public async Task<IActionResult> DeleteConfig(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var cfg = await db.KommoIntegrations.FirstOrDefaultAsync(k => k.EmpresaId == empresaId, ct);
        if (cfg is null) return NoContent();

        db.KommoIntegrations.Remove(cfg);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(empresaId, "kommo.config.delete", "KommoIntegration", cfg.Id, cfg.Subdomain, ct: ct);
        return NoContent();
    }

    // ===== Sync manual =====

    [Authorize]
    [HttpPost("api/empresas/{empresaId:guid}/kommo/sync")]
    public async Task<ActionResult<KommoSyncResponse>> Sync(
        Guid empresaId, [FromBody] KommoSyncRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var cfg = await db.KommoIntegrations.FirstOrDefaultAsync(k => k.EmpresaId == empresaId, ct);
        if (cfg is null) return StatusCode(StatusCodes.Status412PreconditionFailed, new { message = "Kommo não configurado." });

        var token = protector.Decrypt(cfg.AccessTokenEncrypted);
        var limit = Math.Clamp(req.Limit ?? 50, 1, 250);
        var page = Math.Max(req.Page ?? 1, 1);

        List<JsonElement> records;
        try
        {
            records = await kommo.ListLeadsWithContactsAsync(cfg.Subdomain, token, limit, page, req.Query, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kommo sync failed for empresa {EmpresaId}", empresaId);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }

        // Dedup por kommoLeadId já em pending — não recria item se já existe pendente.
        var kommoIds = records
            .Select(r => r.TryGetProperty("lead", out var l) && l.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                ? idEl.GetInt64()
                : (long?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        var existing = kommoIds.Count == 0 ? new HashSet<long>() :
            (await db.KommoInboxItems
                .Where(i => i.EmpresaId == empresaId && i.Status == "pending" && i.KommoLeadId != null && kommoIds.Contains(i.KommoLeadId!.Value))
                .Select(i => i.KommoLeadId!.Value)
                .ToListAsync(ct)).ToHashSet();

        var stored = 0;
        foreach (var raw in records)
        {
            long? kommoId = null;
            if (raw.TryGetProperty("lead", out var leadEl) && leadEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                kommoId = idEl.GetInt64();
            if (kommoId.HasValue && existing.Contains(kommoId.Value)) continue;

            db.KommoInboxItems.Add(new KommoInboxItem
            {
                EmpresaId = empresaId,
                KommoLeadId = kommoId,
                Source = "sync",
                ReceivedAt = DateTime.UtcNow,
                RawJson = raw.GetRawText(),
                Status = "pending",
            });
            stored++;
        }

        var now = DateTime.UtcNow;
        cfg.LastSyncAt = now;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(
            empresaId, "kommo.sync", "KommoIntegration", cfg.Id, cfg.Subdomain,
            extraMeta: new Dictionary<string, object?> { ["received"] = records.Count, ["stored"] = stored }, ct: ct);

        return Ok(new KommoSyncResponse(records.Count, stored, now));
    }

    // ===== Webhook (Kommo bate aqui) =====
    // Anônimo — Kommo não envia JWT. Validação via secret no querystring (configurado pelo usuário).

    [AllowAnonymous]
    [HttpGet("api/empresas/{empresaId:guid}/kommo/webhook")]
    public IActionResult WebhookProbe(Guid empresaId)
    {
        // Probe pra Kommo conferir que a URL responde.
        return Ok(new { ok = true, service = "cadastraai/kommo-webhook", empresaId });
    }

    [AllowAnonymous]
    [HttpPost("api/empresas/{empresaId:guid}/kommo/webhook")]
    public async Task<IActionResult> Webhook(Guid empresaId, [FromQuery] string? secret, CancellationToken ct)
    {
        var cfg = await db.KommoIntegrations.AsNoTracking().FirstOrDefaultAsync(k => k.EmpresaId == empresaId, ct);
        if (cfg is null) return NotFound();

        if (!string.IsNullOrEmpty(cfg.WebhookSecret))
        {
            if (!string.Equals(secret, cfg.WebhookSecret, StringComparison.Ordinal))
                return Unauthorized();
        }

        // Lê o body como string (Kommo manda form-urlencoded; aceitamos JSON também).
        string bodyText;
        using (var reader = new StreamReader(Request.Body))
        {
            bodyText = await reader.ReadToEndAsync(ct);
        }
        var contentType = Request.ContentType ?? "";

        // Normaliza pra JSON: form → objeto recursivo.
        string normalizedJson;
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            normalizedJson = string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText;
        }
        else
        {
            normalizedJson = JsonSerializer.Serialize(ParseFormToObject(bodyText));
        }

        // Tenta extrair leads.add[] / leads.update[] / leads.status[].
        var stored = 0;
        try
        {
            using var doc = JsonDocument.Parse(normalizedJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("leads", out var leadsGroups) && leadsGroups.ValueKind == JsonValueKind.Object)
            {
                foreach (var kind in leadsGroups.EnumerateObject())
                {
                    if (kind.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var entry in kind.Value.EnumerateArray())
                    {
                        long? kid = null;
                        if (entry.TryGetProperty("id", out var idEl))
                        {
                            if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var n)) kid = n;
                            else if (idEl.ValueKind == JsonValueKind.String && long.TryParse(idEl.GetString(), out n)) kid = n;
                        }
                        // Empacota como {lead: entry} pra mesma forma do sync.
                        var wrapped = $"{{\"lead\":{entry.GetRawText()}}}";
                        db.KommoInboxItems.Add(new KommoInboxItem
                        {
                            EmpresaId = empresaId,
                            KommoLeadId = kid,
                            Source = "webhook",
                            ReceivedAt = DateTime.UtcNow,
                            RawJson = wrapped,
                            Status = "pending",
                        });
                        stored++;
                    }
                }
            }

            if (stored == 0)
            {
                // Payload não reconhecido — armazenamos cru pra inspeção.
                db.KommoInboxItems.Add(new KommoInboxItem
                {
                    EmpresaId = empresaId,
                    Source = "webhook",
                    ReceivedAt = DateTime.UtcNow,
                    RawJson = normalizedJson,
                    Status = "pending",
                    Note = "Payload não reconhecido — inspecionar manualmente.",
                });
                stored = 1;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao processar webhook Kommo para empresa {EmpresaId}", empresaId);
            return BadRequest(new { message = "Payload inválido." });
        }

        await audit.LogAsync(empresaId, "kommo.webhook", "KommoInboxItem", null,
            extraMeta: new Dictionary<string, object?> { ["stored"] = stored }, ct: ct);
        return Ok(new { ok = true, stored });
    }

    private static Dictionary<string, object?> ParseFormToObject(string body)
    {
        // "leads[add][0][id]=123&leads[add][0][name]=João" → { leads: { add: [ { id, name } ] } }
        var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var root = new Dictionary<string, object?>();
        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');
            var rawKey = eq >= 0 ? pair[..eq] : pair;
            var rawVal = eq >= 0 ? Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' ')) : "";
            var key = Uri.UnescapeDataString(rawKey);
            var path = ParsePath(key);
            SetByPath(root, path, rawVal);
        }
        return root;
    }

    private static List<string> ParsePath(string raw)
    {
        // "leads[add][0][id]" → ["leads","add","0","id"]
        var path = new List<string>();
        var current = "";
        foreach (var ch in raw)
        {
            if (ch == '[') { if (current.Length > 0) path.Add(current); current = ""; }
            else if (ch == ']') { if (current.Length > 0) path.Add(current); current = ""; }
            else current += ch;
        }
        if (current.Length > 0) path.Add(current);
        return path;
    }

    private static void SetByPath(Dictionary<string, object?> root, List<string> path, string value)
    {
        if (path.Count == 0) return;
        object cursor = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var seg = path[i];
            var next = path[i + 1];
            var nextIsIndex = int.TryParse(next, out _);

            if (cursor is Dictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(seg, out var child) || child is null)
                {
                    child = nextIsIndex ? (object)new List<object?>() : new Dictionary<string, object?>();
                    dict[seg] = child;
                }
                cursor = child;
            }
            else if (cursor is List<object?> list && int.TryParse(seg, out var idx))
            {
                while (list.Count <= idx) list.Add(null);
                if (list[idx] is null) list[idx] = nextIsIndex ? (object)new List<object?>() : new Dictionary<string, object?>();
                cursor = list[idx]!;
            }
        }

        var leaf = path[^1];
        if (cursor is Dictionary<string, object?> d2) d2[leaf] = value;
        else if (cursor is List<object?> l2 && int.TryParse(leaf, out var li))
        {
            while (l2.Count <= li) l2.Add(null);
            l2[li] = value;
        }
    }

    // ===== Inbox =====

    [Authorize]
    [HttpGet("api/empresas/{empresaId:guid}/kommo/inbox")]
    public async Task<ActionResult<List<KommoInboxItemDto>>> ListInbox(
        Guid empresaId, [FromQuery] string? status, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var q = db.KommoInboxItems.AsNoTracking().Where(i => i.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(i => i.Status == status);

        var items = await q
            .OrderByDescending(i => i.ReceivedAt)
            .Take(500)
            .Select(i => new KommoInboxItemDto(
                i.Id, i.EmpresaId, i.KommoLeadId, i.Source, i.ReceivedAt, i.Status,
                i.ImportedLeadId, i.Note, i.RawJson))
            .ToListAsync(ct);
        return Ok(items);
    }

    [Authorize]
    [HttpDelete("api/empresas/{empresaId:guid}/kommo/inbox")]
    public async Task<IActionResult> ClearInbox(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var deleted = await db.KommoInboxItems.Where(i => i.EmpresaId == empresaId).ExecuteDeleteAsync(ct);
        await audit.LogAsync(empresaId, "kommo.inbox.clear", "KommoInboxItem", null,
            extraMeta: new Dictionary<string, object?> { ["deleted"] = deleted }, ct: ct);
        return Ok(new { deleted });
    }

    [Authorize]
    [HttpPatch("api/empresas/{empresaId:guid}/kommo/inbox/{itemId:guid}")]
    public async Task<ActionResult<KommoInboxItemDto>> UpdateInboxItem(
        Guid empresaId, Guid itemId, [FromBody] Dictionary<string, JsonElement> patch, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var item = await db.KommoInboxItems.FirstOrDefaultAsync(i => i.Id == itemId && i.EmpresaId == empresaId, ct);
        if (item is null) return NotFound();

        if (patch.TryGetValue("status", out var stEl) && stEl.ValueKind == JsonValueKind.String)
        {
            var st = stEl.GetString();
            if (st is "pending" or "imported" or "discarded") item.Status = st;
        }
        if (patch.TryGetValue("note", out var nEl) && nEl.ValueKind == JsonValueKind.String)
        {
            item.Note = nEl.GetString();
        }
        await db.SaveChangesAsync(ct);
        return Ok(new KommoInboxItemDto(
            item.Id, item.EmpresaId, item.KommoLeadId, item.Source, item.ReceivedAt, item.Status,
            item.ImportedLeadId, item.Note, item.RawJson));
    }

    [Authorize]
    [HttpDelete("api/empresas/{empresaId:guid}/kommo/inbox/{itemId:guid}")]
    public async Task<IActionResult> DeleteInboxItem(Guid empresaId, Guid itemId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var deleted = await db.KommoInboxItems
            .Where(i => i.Id == itemId && i.EmpresaId == empresaId)
            .ExecuteDeleteAsync(ct);
        return deleted > 0 ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpPost("api/empresas/{empresaId:guid}/kommo/inbox/{itemId:guid}/promote")]
    public async Task<ActionResult<LeadDetailDto>> Promote(
        Guid empresaId, Guid itemId, [FromBody] PromoteKommoLeadRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var item = await db.KommoInboxItems.FirstOrDefaultAsync(i => i.Id == itemId && i.EmpresaId == empresaId, ct);
        if (item is null) return NotFound();
        if (item.Status != "pending") return BadRequest(new { message = "Item já foi processado." });

        var leadReq = req.Lead;
        var lead = new Lead
        {
            EmpresaId = empresaId,
            Nome = leadReq.Nome.Trim(),
            Telefone = leadReq.Telefone.Trim(),
            Origem = string.IsNullOrWhiteSpace(leadReq.Origem) ? "Kommo" : leadReq.Origem.Trim(),
            Tipo = string.IsNullOrWhiteSpace(leadReq.Tipo) ? "Cadastro" : leadReq.Tipo.Trim(),
            TipoResgate = leadReq.TipoResgate?.Trim(),
            Interacao = leadReq.Interacao,
            AgendouConsulta = leadReq.AgendouConsulta,
            PagamentoAntecipado = leadReq.PagamentoAntecipado,
            DataAgendamento = leadReq.DataAgendamento,
            MotivoNaoAgendamento = leadReq.MotivoNaoAgendamento?.Trim(),
            NomeResponsavel = leadReq.NomeResponsavel.Trim(),
            CreatedAt = DateTime.UtcNow,
            Importado = true,
            CreatedByUserId = userId,
        };
        db.Leads.Add(lead);
        item.Status = "imported";
        item.ImportedLeadId = lead.Id;
        item.ImportedAt = DateTime.UtcNow;
        item.ImportedByUserId = userId;
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(empresaId, ct);
        await audit.LogAsync(empresaId, "kommo.promote", "Lead", lead.Id, lead.Nome,
            extraMeta: new Dictionary<string, object?> { ["inboxItemId"] = item.Id, ["kommoLeadId"] = item.KommoLeadId }, ct: ct);

        return Ok(LeadsController.MapDetail(lead));
    }
}
