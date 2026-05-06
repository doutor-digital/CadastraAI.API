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
[Route("api/empresas/{empresaId:guid}/motivos-nao-fechamento")]
public class MotivosNaoFechamentoController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MotivoNaoFechamentoDto>>> List(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var motivos = await db.MotivosNaoFechamento.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId)
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.Nome)
            .Select(m => new MotivoNaoFechamentoDto(
                m.Id, m.EmpresaId, m.Nome, m.Cor, m.IsDefault, m.CreatedAt))
            .ToListAsync(ct);

        return Ok(motivos);
    }

    [HttpPost]
    public async Task<ActionResult<MotivoNaoFechamentoDto>> Create(
        Guid empresaId, [FromBody] CreateMotivoNaoFechamentoRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var nome = req.Nome.Trim();
        var cor = req.Cor.Trim().ToLowerInvariant();
        if (!MotivoNaoFechamento.CoresValidas.Contains(cor))
        {
            return BadRequest(new { message = "Cor inválida. Use 'verde', 'amarelo' ou 'vermelho'." });
        }

        var jaExiste = await db.MotivosNaoFechamento.AnyAsync(
            m => m.EmpresaId == empresaId && m.Nome == nome, ct);
        if (jaExiste)
        {
            return Conflict(new { message = "Já existe um motivo com esse nome." });
        }

        var motivo = new MotivoNaoFechamento
        {
            EmpresaId = empresaId,
            Nome = nome,
            Cor = cor,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.MotivosNaoFechamento.Add(motivo);
        await db.SaveChangesAsync(ct);

        return Ok(new MotivoNaoFechamentoDto(
            motivo.Id, motivo.EmpresaId, motivo.Nome, motivo.Cor, motivo.IsDefault, motivo.CreatedAt));
    }

    [HttpDelete("{motivoId:guid}")]
    public async Task<IActionResult> Delete(Guid empresaId, Guid motivoId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var motivo = await db.MotivosNaoFechamento
            .FirstOrDefaultAsync(m => m.Id == motivoId && m.EmpresaId == empresaId, ct);
        if (motivo is null) return NotFound();
        if (motivo.IsDefault)
        {
            return BadRequest(new { message = "Motivos padrão não podem ser removidos." });
        }

        db.MotivosNaoFechamento.Remove(motivo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
