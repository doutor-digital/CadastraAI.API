using CadastraAI.API.Data;
using CadastraAI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Auth;

public static class MembershipGuard
{
    public static Task<Membership?> Find(AppDbContext db, Guid empresaId, Guid userId, CancellationToken ct) =>
        db.Memberships.FirstOrDefaultAsync(m => m.EmpresaId == empresaId && m.UserId == userId, ct);

    public static async Task<Guid?> EmpresaIdForLead(AppDbContext db, Guid leadId, Guid userId, CancellationToken ct)
    {
        var row = await db.Leads.AsNoTracking()
            .Where(l => l.Id == leadId)
            .Select(l => new { l.EmpresaId })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var hasMembership = await db.Memberships.AnyAsync(
            m => m.EmpresaId == row.EmpresaId && m.UserId == userId, ct);
        return hasMembership ? row.EmpresaId : null;
    }

    public static async Task<Guid?> EmpresaIdForConsulta(AppDbContext db, Guid consultaId, Guid userId, CancellationToken ct)
    {
        var row = await db.Consultas.AsNoTracking()
            .Where(c => c.Id == consultaId)
            .Select(c => new { c.Lead.EmpresaId })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var hasMembership = await db.Memberships.AnyAsync(
            m => m.EmpresaId == row.EmpresaId && m.UserId == userId, ct);
        return hasMembership ? row.EmpresaId : null;
    }

    public static async Task<Guid?> EmpresaIdForTratamento(AppDbContext db, Guid tratamentoId, Guid userId, CancellationToken ct)
    {
        var row = await db.Tratamentos.AsNoTracking()
            .Where(t => t.Id == tratamentoId)
            .Select(t => new { t.Consulta.Lead.EmpresaId })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var hasMembership = await db.Memberships.AnyAsync(
            m => m.EmpresaId == row.EmpresaId && m.UserId == userId, ct);
        return hasMembership ? row.EmpresaId : null;
    }
}
