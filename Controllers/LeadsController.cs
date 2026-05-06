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
