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
public class RecebimentosController(AppDbContext db, IDashboardCache cache) : ControllerBase
{
    private const int MaxRecebimentosConsulta = 2;
    private const int MaxRecebimentosTratamento = 6;

    [HttpPost("api/consultas/{consultaId:guid}/recebimentos")]
    public async Task<ActionResult<RecebimentoDto>> CreateForConsulta(
        Guid consultaId, [FromBody] CreateRecebimentoRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var consulta = await db.Consultas.Include(c => c.Lead).FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null) return NotFound(new { message = "Consulta não encontrada." });
        if (await MembershipGuard.Find(db, consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var count = await db.Recebimentos.CountAsync(r => r.ConsultaId == consultaId, ct);
        if (count >= MaxRecebimentosConsulta)
        {
            return BadRequest(new { message = $"Máximo de {MaxRecebimentosConsulta} recebimentos por consulta." });
        }

        var recebimento = new Recebimento
        {
            ConsultaId = consultaId,
            ValorRecebimento = req.ValorRecebimento,
            FormaPagamento = req.FormaPagamento.Trim(),
            DataRecebimento = req.DataRecebimento,
        };
        db.Recebimentos.Add(recebimento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(consulta.Lead.EmpresaId, ct);

        return Ok(LeadsController.MapRecebimento(recebimento));
    }

    [HttpPost("api/tratamentos/{tratamentoId:guid}/recebimentos")]
    public async Task<ActionResult<RecebimentoDto>> CreateForTratamento(
        Guid tratamentoId, [FromBody] CreateRecebimentoRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var tratamento = await db.Tratamentos
            .Include(t => t.Consulta).ThenInclude(c => c.Lead)
            .FirstOrDefaultAsync(t => t.Id == tratamentoId, ct);
        if (tratamento is null) return NotFound(new { message = "Tratamento não encontrado." });
        if (await MembershipGuard.Find(db, tratamento.Consulta.Lead.EmpresaId, userId.Value, ct) is null) return Forbid();

        var count = await db.Recebimentos.CountAsync(r => r.TratamentoId == tratamentoId, ct);
        if (count >= MaxRecebimentosTratamento)
        {
            return BadRequest(new { message = $"Máximo de {MaxRecebimentosTratamento} recebimentos por tratamento." });
        }

        var recebimento = new Recebimento
        {
            TratamentoId = tratamentoId,
            ValorRecebimento = req.ValorRecebimento,
            FormaPagamento = req.FormaPagamento.Trim(),
            DataRecebimento = req.DataRecebimento,
        };
        db.Recebimentos.Add(recebimento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(tratamento.Consulta.Lead.EmpresaId, ct);

        return Ok(LeadsController.MapRecebimento(recebimento));
    }

    [HttpDelete("api/recebimentos/{recebimentoId:guid}")]
    public async Task<IActionResult> Delete(Guid recebimentoId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var recebimento = await db.Recebimentos
            .Include(r => r.Consulta).ThenInclude(c => c!.Lead)
            .Include(r => r.Tratamento).ThenInclude(t => t!.Consulta).ThenInclude(c => c.Lead)
            .FirstOrDefaultAsync(r => r.Id == recebimentoId, ct);
        if (recebimento is null) return NotFound();

        var empresaId = recebimento.Consulta?.Lead.EmpresaId
                        ?? recebimento.Tratamento?.Consulta.Lead.EmpresaId;
        if (empresaId is null) return NotFound();
        if (await MembershipGuard.Find(db, empresaId.Value, userId.Value, ct) is null) return Forbid();

        db.Recebimentos.Remove(recebimento);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateEmpresaAsync(empresaId.Value, ct);
        return NoContent();
    }
}
