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
public class TratamentosController(AppDbContext db, IDashboardCache cache) : ControllerBase
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
        };
        db.Tratamentos.Add(tratamento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(consulta.Lead.EmpresaId, ct);

        tratamento = await db.Tratamentos.AsNoTracking()
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

        if (req.PlanoTratamento is not null) tratamento.PlanoTratamento = req.PlanoTratamento.Trim();
        if (req.PlanoPilates is not null) tratamento.PlanoPilates = req.PlanoPilates.Trim();
        if (req.Musculacao is not null) tratamento.Musculacao = req.Musculacao.Trim();
        if (req.Procedimento is not null) tratamento.Procedimento = req.Procedimento.Trim();
        if (req.ValorPlano is not null) tratamento.ValorPlano = req.ValorPlano.Value;

        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(tratamento.Consulta.Lead.EmpresaId, ct);
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
        db.Tratamentos.Remove(tratamento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(empresaId, ct);
        return NoContent();
    }
}
