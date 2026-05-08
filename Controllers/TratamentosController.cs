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
public class TratamentosController(AppDbContext db, IDashboardCache cache, IAuditLogger audit) : ControllerBase
{
    [HttpPost("api/consultas/{consultaId:guid}/tratamento")]
    public async Task<ActionResult<TratamentoDto>> Create(
        Guid consultaId, [FromBody] CreateTratamentoRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var consulta = await db.Consultas.Include(c => c.Lead).FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null) return NotFound(new { message = "Consulta não encontrada." });
        if (await MembershipGuard.Find(db, consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var existing = await db.Tratamentos.AnyAsync(t => t.ConsultaId == consultaId, ct);
        if (existing) return Conflict(new { message = "Essa consulta já tem tratamento cadastrado." });

        var tratamento = new Tratamento
        {
            ConsultaId = consultaId,
            PlanoTratamento = req.PlanoTratamento.Trim(),
            PlanoPilates = req.PlanoPilates?.Trim(),
            Musculacao = req.Musculacao?.Trim(),
            Procedimento = req.Procedimento?.Trim(),
            ValorPlano = req.ValorPlano,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        db.Tratamentos.Add(tratamento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(consulta.Lead.EmpresaId, ct);
        await audit.LogAsync(consulta.Lead.EmpresaId, "tratamento.create", "Tratamento", tratamento.Id, consulta.Lead.Nome, ct: ct);

        tratamento = await db.Tratamentos.AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Recebimentos)
            .FirstAsync(t => t.Id == tratamento.Id, ct);

        return Ok(LeadsController.MapTratamento(tratamento));
    }

    [HttpPatch("api/tratamentos/{tratamentoId:guid}")]
    public async Task<ActionResult<TratamentoDto>> Update(
        Guid tratamentoId, [FromBody] UpdateTratamentoRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var tratamento = await db.Tratamentos
            .Include(t => t.Recebimentos)
            .Include(t => t.Consulta).ThenInclude(c => c.Lead)
            .FirstOrDefaultAsync(t => t.Id == tratamentoId, ct);
        if (tratamento is null) return NotFound();
        if (await MembershipGuard.Find(db, tratamento.Consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var changed = new List<string>();
        if (req.PlanoTratamento is not null && tratamento.PlanoTratamento != req.PlanoTratamento.Trim()) { tratamento.PlanoTratamento = req.PlanoTratamento.Trim(); changed.Add(nameof(Tratamento.PlanoTratamento)); }
        if (req.PlanoPilates is not null && tratamento.PlanoPilates != req.PlanoPilates.Trim()) { tratamento.PlanoPilates = req.PlanoPilates.Trim(); changed.Add(nameof(Tratamento.PlanoPilates)); }
        if (req.Musculacao is not null && tratamento.Musculacao != req.Musculacao.Trim()) { tratamento.Musculacao = req.Musculacao.Trim(); changed.Add(nameof(Tratamento.Musculacao)); }
        if (req.Procedimento is not null && tratamento.Procedimento != req.Procedimento.Trim()) { tratamento.Procedimento = req.Procedimento.Trim(); changed.Add(nameof(Tratamento.Procedimento)); }
        if (req.ValorPlano is not null && tratamento.ValorPlano != req.ValorPlano.Value) { tratamento.ValorPlano = req.ValorPlano.Value; changed.Add(nameof(Tratamento.ValorPlano)); }

        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(tratamento.Consulta.Lead.EmpresaId, ct);
        if (changed.Count > 0)
        {
            await audit.LogAsync(tratamento.Consulta.Lead.EmpresaId, "tratamento.update", "Tratamento", tratamento.Id, tratamento.Consulta.Lead.Nome, changedFields: changed, ct: ct);
        }
        return Ok(LeadsController.MapTratamento(tratamento));
    }

    [HttpDelete("api/tratamentos/{tratamentoId:guid}")]
    public async Task<IActionResult> Delete(Guid tratamentoId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var tratamento = await db.Tratamentos
            .Include(t => t.Consulta).ThenInclude(c => c.Lead)
            .FirstOrDefaultAsync(t => t.Id == tratamentoId, ct);
        if (tratamento is null) return NotFound();
        if (await MembershipGuard.Find(db, tratamento.Consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var empresaId = tratamento.Consulta.Lead.EmpresaId;
        var leadNome = tratamento.Consulta.Lead.Nome;
        db.Tratamentos.Remove(tratamento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(empresaId, ct);
        await audit.LogAsync(empresaId, "tratamento.delete", "Tratamento", tratamentoId, leadNome, ct: ct);
        return NoContent();
    }
}
