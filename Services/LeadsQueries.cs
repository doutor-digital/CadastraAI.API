using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Services;

/// Read-side queries shared by LeadsController and DashboardController.
/// Pure data access — no auth checks; callers must guard membership.
public static class LeadsQueries
{
    public static async Task<PaginatedLeadsResponse> ListAsync(
        AppDbContext db,
        Guid empresaId,
        int page,
        int pageSize,
        string? search,
        string? status,
        CancellationToken ct)
    {
        var q = db.Leads.AsNoTracking().Where(l => l.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(l =>
                l.Nome.ToLower().Contains(s)
                || l.Telefone.ToLower().Contains(s)
                || l.Origem.ToLower().Contains(s)
                || l.NomeResponsavel.ToLower().Contains(s));
        }

        if (status == "agendados") q = q.Where(l => l.AgendouConsulta);
        else if (status == "nao_agendados") q = q.Where(l => !l.AgendouConsulta);

        var total = await q.CountAsync(ct);

        var items = await q
            .Include(l => l.Consulta)
            .OrderByDescending(l => l.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
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

        return new PaginatedLeadsResponse(items, total, page, pageSize);
    }

    public static async Task<LeadsStatsResponse> StatsAsync(
        AppDbContext db,
        Guid empresaId,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime? prevFromUtc,
        DateTime? prevToUtc,
        CancellationToken ct)
    {
        var current = await ComputePeriodAsync(db, empresaId, fromUtc, toUtc, ct);

        LeadsStatsPeriod? previous = null;
        if (prevFromUtc.HasValue && prevToUtc.HasValue)
        {
            previous = await ComputePeriodAsync(db, empresaId, prevFromUtc.Value, prevToUtc.Value, ct);
        }

        var origens = await db.Leads.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.CreatedAt >= fromUtc && l.CreatedAt < toUtc)
            .GroupBy(l => l.Origem)
            .Select(g => new OrigemBucket(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var responsaveis = await db.Leads.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.CreatedAt >= fromUtc && l.CreatedAt < toUtc)
            .GroupBy(l => l.NomeResponsavel)
            .Select(g => new ResponsavelBucket(
                g.Key,
                g.Count(),
                g.Count(l => l.Consulta != null && l.Consulta.Compareceu),
                g.Count(l => l.Consulta != null && l.Consulta.FechouTratamento)))
            .OrderByDescending(x => x.Fecharam)
            .ThenByDescending(x => x.Leads)
            .Take(10)
            .ToListAsync(ct);

        return new LeadsStatsResponse(current, previous, origens, responsaveis);
    }

    private static async Task<LeadsStatsPeriod> ComputePeriodAsync(
        AppDbContext db, Guid empresaId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var agg = await db.Leads.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.CreatedAt >= fromUtc && l.CreatedAt < toUtc)
            .GroupBy(l => 1)
            .Select(g => new
            {
                Leads = g.Count(),
                Agendados = g.Count(l => l.AgendouConsulta),
                ComConsulta = g.Count(l => l.Consulta != null),
                Compareceram = g.Count(l => l.Consulta != null && l.Consulta.Compareceu),
                Fecharam = g.Count(l => l.Consulta != null && l.Consulta.FechouTratamento),
                Cadastros = g.Count(l => l.Tipo == "Cadastro"),
                Resgates = g.Count(l => l.Tipo == "Resgate"),
            })
            .FirstOrDefaultAsync(ct);

        return agg is null
            ? new LeadsStatsPeriod(0, 0, 0, 0, 0, 0, 0)
            : new LeadsStatsPeriod(agg.Leads, agg.Agendados, agg.ComConsulta, agg.Compareceram, agg.Fecharam, agg.Cadastros, agg.Resgates);
    }

    public static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt
        : dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime()
        : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
