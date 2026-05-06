using CadastraAI.API.Auth;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Authorize]
public class LeadsController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/empresas/{empresaId:guid}/leads")]
    public async Task<ActionResult<List<LeadSummaryDto>>> List(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var leads = await db.Leads.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .Include(l => l.Consulta)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeadSummaryDto(
                l.Id,
                l.EmpresaId,
                l.Nome,
                l.Telefone,
                l.Origem,
                l.Tipo,
                l.TipoResgate,
                l.Interacao,
                l.AgendouConsulta,
                l.PagamentoAntecipado,
                l.DataAgendamento,
                l.MotivoNaoAgendamento,
                l.NomeResponsavel,
                l.CreatedAt,
                l.Consulta != null,
                l.Consulta != null ? l.Consulta.Compareceu : null,
                l.Consulta != null ? l.Consulta.FechouTratamento : null,
                l.Consulta != null ? l.Consulta.MotivoNaoFechamento : null))
            .ToListAsync(ct);

        return Ok(leads);
    }

    [HttpGet("api/leads/{leadId:guid}")]
    public async Task<ActionResult<LeadDetailDto>> Get(Guid leadId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var lead = await db.Leads.AsNoTracking()
            .Include(l => l.Consulta).ThenInclude(c => c!.Tratamento).ThenInclude(t => t!.Recebimentos)
            .Include(l => l.Consulta).ThenInclude(c => c!.Recebimentos)
            .FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return NotFound();

        if (await MembershipGuard.Find(db, lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        return Ok(MapDetail(lead));
    }

    [HttpPost("api/empresas/{empresaId:guid}/leads")]
    public async Task<ActionResult<LeadDetailDto>> Create(
        Guid empresaId, [FromBody] CreateLeadRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var lead = new Lead
        {
            EmpresaId = empresaId,
            Nome = req.Nome.Trim(),
            Telefone = req.Telefone.Trim(),
            Origem = req.Origem.Trim(),
            Tipo = req.Tipo.Trim(),
            TipoResgate = req.TipoResgate?.Trim(),
            Interacao = req.Interacao,
            AgendouConsulta = req.AgendouConsulta,
            PagamentoAntecipado = req.PagamentoAntecipado,
            DataAgendamento = req.DataAgendamento,
            MotivoNaoAgendamento = req.MotivoNaoAgendamento?.Trim(),
            NomeResponsavel = req.NomeResponsavel.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        return Ok(MapDetail(lead));
    }

    [HttpPost("api/empresas/{empresaId:guid}/leads/bulk")]
    public async Task<ActionResult<BulkCreateLeadsResponse>> BulkCreate(
        Guid empresaId, [FromBody] BulkCreateLeadsRequest req, CancellationToken ct)
    {
        const int MaxBatchSize = 5000;

        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        if (req.Leads is null || req.Leads.Count == 0)
            return BadRequest(new { message = "Nenhum lead enviado." });
        if (req.Leads.Count > MaxBatchSize)
            return BadRequest(new { message = $"Máximo de {MaxBatchSize} leads por importação." });

        var failed = new List<BulkLeadErrorDto>();
        var pending = new List<Lead>(req.Leads.Count);
        var pendingIndex = new List<int>(req.Leads.Count);
        var now = DateTime.UtcNow;

        static DateTime? NormalizeUtc(DateTime? dt)
        {
            if (dt is null) return null;
            return dt.Value.Kind switch
            {
                DateTimeKind.Utc => dt.Value,
                DateTimeKind.Local => dt.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc),
            };
        }

        for (var i = 0; i < req.Leads.Count; i++)
        {
            var item = req.Leads[i];
            try
            {
                if (item is null) throw new ArgumentException("Linha vazia.");
                if (string.IsNullOrWhiteSpace(item.Nome)) throw new ArgumentException("Nome é obrigatório.");
                if (string.IsNullOrWhiteSpace(item.Telefone)) throw new ArgumentException("Telefone é obrigatório.");
                if (string.IsNullOrWhiteSpace(item.Origem)) throw new ArgumentException("Origem é obrigatória.");
                if (string.IsNullOrWhiteSpace(item.Tipo)) throw new ArgumentException("Tipo é obrigatório.");
                if (string.IsNullOrWhiteSpace(item.NomeResponsavel)) throw new ArgumentException("Responsável é obrigatório.");

                pending.Add(new Lead
                {
                    EmpresaId = empresaId,
                    Nome = item.Nome.Trim(),
                    Telefone = item.Telefone.Trim(),
                    Origem = item.Origem.Trim(),
                    Tipo = item.Tipo.Trim(),
                    TipoResgate = item.TipoResgate?.Trim(),
                    Interacao = item.Interacao,
                    AgendouConsulta = item.AgendouConsulta,
                    PagamentoAntecipado = item.PagamentoAntecipado,
                    DataAgendamento = NormalizeUtc(item.DataAgendamento),
                    MotivoNaoAgendamento = item.MotivoNaoAgendamento?.Trim(),
                    NomeResponsavel = item.NomeResponsavel.Trim(),
                    CreatedAt = now,
                });
                pendingIndex.Add(i);
            }
            catch (Exception ex)
            {
                failed.Add(new BulkLeadErrorDto(i, ex.Message));
            }
        }

        var saved = new List<Lead>(pending.Count);
        if (pending.Count > 0)
        {
            // Fast path: tenta o batch inteiro em uma transação.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.Leads.AddRange(pending);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                saved.AddRange(pending);
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                // Fallback: detach tudo e tenta linha por linha pra isolar quem falha.
                foreach (var entry in db.ChangeTracker.Entries<Lead>().ToList())
                    entry.State = EntityState.Detached;

                for (var k = 0; k < pending.Count; k++)
                {
                    var lead = pending[k];
                    var originalIdx = pendingIndex[k];
                    try
                    {
                        db.Leads.Add(lead);
                        await db.SaveChangesAsync(ct);
                        db.Entry(lead).State = EntityState.Detached;
                        saved.Add(lead);
                    }
                    catch (Exception rowEx)
                    {
                        db.Entry(lead).State = EntityState.Detached;
                        var msg = (rowEx.GetBaseException() ?? rowEx).Message;
                        failed.Add(new BulkLeadErrorDto(originalIdx, $"Falha ao gravar: {msg}"));
                    }
                }
            }
        }

        var created = saved.Select(MapDetail).ToList();
        return Ok(new BulkCreateLeadsResponse(
            TotalReceived: req.Leads.Count,
            CreatedCount: created.Count,
            FailedCount: failed.Count,
            Created: created,
            Failed: failed));
    }

    [HttpPatch("api/leads/{leadId:guid}")]
    public async Task<ActionResult<LeadDetailDto>> Update(
        Guid leadId, [FromBody] UpdateLeadRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var lead = await db.Leads
            .Include(l => l.Consulta).ThenInclude(c => c!.Tratamento).ThenInclude(t => t!.Recebimentos)
            .Include(l => l.Consulta).ThenInclude(c => c!.Recebimentos)
            .FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return NotFound();
        if (await MembershipGuard.Find(db, lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        if (req.Nome is not null) lead.Nome = req.Nome.Trim();
        if (req.Telefone is not null) lead.Telefone = req.Telefone.Trim();
        if (req.Origem is not null) lead.Origem = req.Origem.Trim();
        if (req.Tipo is not null) lead.Tipo = req.Tipo.Trim();
        if (req.TipoResgate is not null) lead.TipoResgate = req.TipoResgate.Trim();
        if (req.Interacao is not null) lead.Interacao = req.Interacao.Value;
        if (req.AgendouConsulta is not null) lead.AgendouConsulta = req.AgendouConsulta.Value;
        if (req.PagamentoAntecipado is not null) lead.PagamentoAntecipado = req.PagamentoAntecipado.Value;
        if (req.DataAgendamento is not null) lead.DataAgendamento = req.DataAgendamento;
        if (req.MotivoNaoAgendamento is not null) lead.MotivoNaoAgendamento = req.MotivoNaoAgendamento.Trim();
        if (req.NomeResponsavel is not null) lead.NomeResponsavel = req.NomeResponsavel.Trim();

        await db.SaveChangesAsync(ct);
        return Ok(MapDetail(lead));
    }

    [HttpDelete("api/leads/{leadId:guid}")]
    public async Task<IActionResult> Delete(Guid leadId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return NotFound();
        if (await MembershipGuard.Find(db, lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        db.Leads.Remove(lead);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    internal static LeadDetailDto MapDetail(Lead l) => new(
        l.Id,
        l.EmpresaId,
        l.Nome,
        l.Telefone,
        l.Origem,
        l.Tipo,
        l.TipoResgate,
        l.Interacao,
        l.AgendouConsulta,
        l.PagamentoAntecipado,
        l.DataAgendamento,
        l.MotivoNaoAgendamento,
        l.NomeResponsavel,
        l.CreatedAt,
        l.Consulta is null ? null : MapConsulta(l.Consulta));

    internal static ConsultaDto MapConsulta(Consulta c) => new(
        c.Id,
        c.LeadId,
        c.ValorConsulta,
        c.PagamentoAntecipado,
        c.TratamentoIndicado,
        c.Orcamento,
        c.Compareceu,
        c.FechouTratamento,
        c.MotivoNaoFechamento,
        c.CreatedAt,
        c.Tratamento is null ? null : MapTratamento(c.Tratamento),
        c.Recebimentos.Select(MapRecebimento).ToList());

    internal static TratamentoDto MapTratamento(Tratamento t) => new(
        t.Id,
        t.ConsultaId,
        t.PlanoTratamento,
        t.PlanoPilates,
        t.Musculacao,
        t.Procedimento,
        t.ValorPlano,
        t.CreatedAt,
        t.Recebimentos.Select(MapRecebimento).ToList());

    internal static RecebimentoDto MapRecebimento(Recebimento r) => new(
        r.Id,
        r.ConsultaId,
        r.TratamentoId,
        r.ValorRecebimento,
        r.FormaPagamento,
        r.DataRecebimento);
}
