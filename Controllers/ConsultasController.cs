using CadastraAI.API.Audit;
using CadastraAI.API.Auth;
using CadastraAI.API.Cache;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Authorize]
public class ConsultasController(AppDbContext db, IDashboardCache cache, IAuditLogger audit) : ControllerBase
{
    [HttpPost("api/leads/{leadId:guid}/consulta")]
    public async Task<ActionResult<ConsultaDto>> Create(
        Guid leadId, [FromBody] CreateConsultaRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return NotFound(new { message = "Lead não encontrado." });
        if (await MembershipGuard.Find(db, lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var existing = await db.Consultas.AnyAsync(c => c.LeadId == leadId, ct);
        if (existing) return Conflict(new { message = "Esse lead já tem consulta cadastrada." });

        if (!string.IsNullOrEmpty(req.MotivoNaoFechamento))
        {
            var motivoExiste = await db.MotivosNaoFechamento.AnyAsync(
                m => m.EmpresaId == lead.EmpresaId && m.Nome == req.MotivoNaoFechamento, ct);
            if (!motivoExiste)
            {
                return BadRequest(new { message = "Motivo de não fechamento inválido para esta empresa." });
            }
        }

        var consulta = new Consulta
        {
            LeadId = leadId,
            ValorConsulta = req.ValorConsulta,
            PagamentoAntecipado = req.PagamentoAntecipado,
            TratamentoIndicado = req.TratamentoIndicado.Trim(),
            Orcamento = req.Orcamento,
            Compareceu = req.Compareceu,
            FechouTratamento = req.FechouTratamento,
            MotivoNaoFechamento = string.IsNullOrEmpty(req.MotivoNaoFechamento) ? null : req.MotivoNaoFechamento.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        db.Consultas.Add(consulta);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(lead.EmpresaId, ct);
        await audit.LogAsync(lead.EmpresaId, "consulta.create", "Consulta", consulta.Id, lead.Nome, ct: ct);

        consulta = await db.Consultas.AsNoTracking()
            .Include(c => c.CreatedBy)
            .Include(c => c.Tratamento).ThenInclude(t => t!.Recebimentos)
            .Include(c => c.Recebimentos)
            .FirstAsync(c => c.Id == consulta.Id, ct);

        return Ok(LeadsController.MapConsulta(consulta));
    }

    [HttpPatch("api/consultas/{consultaId:guid}")]
    public async Task<ActionResult<ConsultaDto>> Update(
        Guid consultaId, [FromBody] UpdateConsultaRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var consulta = await db.Consultas
            .Include(c => c.Tratamento).ThenInclude(t => t!.Recebimentos)
            .Include(c => c.Recebimentos)
            .Include(c => c.Lead)
            .FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null) return NotFound();

        if (await MembershipGuard.Find(db, consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        if (req.MotivoNaoFechamento is not null && !string.IsNullOrEmpty(req.MotivoNaoFechamento))
        {
            var motivoExiste = await db.MotivosNaoFechamento.AnyAsync(
                m => m.EmpresaId == consulta.Lead.EmpresaId && m.Nome == req.MotivoNaoFechamento, ct);
            if (!motivoExiste)
            {
                return BadRequest(new { message = "Motivo de não fechamento inválido para esta empresa." });
            }
        }

        var changed = new List<string>();
        if (req.ValorConsulta is not null && consulta.ValorConsulta != req.ValorConsulta.Value) { consulta.ValorConsulta = req.ValorConsulta.Value; changed.Add(nameof(Consulta.ValorConsulta)); }
        if (req.PagamentoAntecipado is not null && consulta.PagamentoAntecipado != req.PagamentoAntecipado.Value) { consulta.PagamentoAntecipado = req.PagamentoAntecipado.Value; changed.Add(nameof(Consulta.PagamentoAntecipado)); }
        if (req.TratamentoIndicado is not null && consulta.TratamentoIndicado != req.TratamentoIndicado.Trim()) { consulta.TratamentoIndicado = req.TratamentoIndicado.Trim(); changed.Add(nameof(Consulta.TratamentoIndicado)); }
        if (req.Orcamento is not null && consulta.Orcamento != req.Orcamento.Value) { consulta.Orcamento = req.Orcamento.Value; changed.Add(nameof(Consulta.Orcamento)); }
        if (req.Compareceu is not null && consulta.Compareceu != req.Compareceu.Value) { consulta.Compareceu = req.Compareceu.Value; changed.Add(nameof(Consulta.Compareceu)); }
        if (req.FechouTratamento is not null && consulta.FechouTratamento != req.FechouTratamento.Value) { consulta.FechouTratamento = req.FechouTratamento.Value; changed.Add(nameof(Consulta.FechouTratamento)); }
        if (req.MotivoNaoFechamento is not null)
        {
            var newVal = string.IsNullOrEmpty(req.MotivoNaoFechamento) ? null : req.MotivoNaoFechamento.Trim();
            if (consulta.MotivoNaoFechamento != newVal) { consulta.MotivoNaoFechamento = newVal; changed.Add(nameof(Consulta.MotivoNaoFechamento)); }
        }

        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(consulta.Lead.EmpresaId, ct);
        if (changed.Count > 0)
        {
            await audit.LogAsync(consulta.Lead.EmpresaId, "consulta.update", "Consulta", consulta.Id, consulta.Lead.Nome, changedFields: changed, ct: ct);
        }
        return Ok(LeadsController.MapConsulta(consulta));
    }

    [HttpDelete("api/consultas/{consultaId:guid}")]
    public async Task<IActionResult> Delete(Guid consultaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var consulta = await db.Consultas.Include(c => c.Lead).FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null) return NotFound();
        if (await MembershipGuard.Find(db, consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var empresaId = consulta.Lead.EmpresaId;
        var leadNome = consulta.Lead.Nome;
        db.Consultas.Remove(consulta);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(empresaId, ct);
        await audit.LogAsync(empresaId, "consulta.delete", "Consulta", consultaId, leadNome, ct: ct);
        return NoContent();
    }
}
