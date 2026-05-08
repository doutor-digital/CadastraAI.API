using System.Text.Json;
using CadastraAI.API.Auth;
using CadastraAI.API.Cache;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Models;
using CadastraAI.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Authorize]
public class DashboardController(AppDbContext db, IDashboardCache cache) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// Botão "Sincronizar": invalida o cache da empresa, recalcula stats frescos
    /// e cria automaticamente um snapshot do dashboard.
    [HttpPost("api/empresas/{empresaId:guid}/dashboard/sync")]
    public async Task<ActionResult<DashboardSyncResponse>> Sync(
        Guid empresaId, [FromBody] DashboardSyncRequest? req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        await cache.InvalidateEmpresaAsync(empresaId, ct);

        var (fromUtc, toUtc, prevFromUtc, prevToUtc) = ResolvePeriod(req?.From, req?.To, req?.PrevFrom, req?.PrevTo);

        var stats = await LeadsQueries.StatsAsync(db, empresaId, fromUtc, toUtc, prevFromUtc, prevToUtc, ct);

        var snapshot = await PersistSnapshotAsync(empresaId, userId.Value, fromUtc, toUtc, "sync", stats, ct);

        return Ok(new DashboardSyncResponse(stats, ToDto(snapshot, stats)));
    }

    /// Captura manual a partir de um botão dedicado em Relatórios / dashboard.
    [HttpPost("api/empresas/{empresaId:guid}/dashboard/snapshots")]
    public async Task<ActionResult<DashboardSnapshotDto>> CreateSnapshot(
        Guid empresaId, [FromBody] CreateSnapshotRequest? req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        var (fromUtc, toUtc, prevFromUtc, prevToUtc) = ResolvePeriod(req?.From, req?.To, req?.PrevFrom, req?.PrevTo);

        var stats = await LeadsQueries.StatsAsync(db, empresaId, fromUtc, toUtc, prevFromUtc, prevToUtc, ct);
        var snapshot = await PersistSnapshotAsync(empresaId, userId.Value, fromUtc, toUtc, "manual", stats, ct);

        return Ok(ToDto(snapshot, stats));
    }

    [HttpGet("api/empresas/{empresaId:guid}/dashboard/snapshots")]
    public async Task<ActionResult<List<DashboardSnapshotSummaryDto>>> ListSnapshots(
        Guid empresaId,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await MembershipGuard.Find(db, empresaId, userId.Value, ct) is null) return Forbid();

        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(0, page);

        var items = await db.DashboardSnapshots.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .OrderByDescending(s => s.CapturedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(s => new DashboardSnapshotSummaryDto(
                s.Id,
                s.EmpresaId,
                s.CapturedByUserId,
                s.CapturedBy.Name,
                s.CapturedAt,
                s.PeriodFrom,
                s.PeriodTo,
                s.Source))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("api/dashboard/snapshots/{snapshotId:guid}")]
    public async Task<ActionResult<DashboardSnapshotDto>> GetSnapshot(Guid snapshotId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var snapshot = await db.DashboardSnapshots.AsNoTracking()
            .Include(s => s.CapturedBy)
            .FirstOrDefaultAsync(s => s.Id == snapshotId, ct);
        if (snapshot is null) return NotFound();
        if (await MembershipGuard.Find(db, snapshot.EmpresaId, userId.Value, ct) is null) return Forbid();

        var stats = JsonSerializer.Deserialize<LeadsStatsResponse>(snapshot.PayloadJson, JsonOpts)
                    ?? new LeadsStatsResponse(new LeadsStatsPeriod(0,0,0,0,0,0,0), null, [], []);

        return Ok(ToDto(snapshot, stats));
    }

    [HttpDelete("api/dashboard/snapshots/{snapshotId:guid}")]
    public async Task<IActionResult> DeleteSnapshot(Guid snapshotId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var snapshot = await db.DashboardSnapshots.FirstOrDefaultAsync(s => s.Id == snapshotId, ct);
        if (snapshot is null) return NotFound();
        if (await MembershipGuard.Find(db, snapshot.EmpresaId, userId.Value, ct) is null) return Forbid();

        db.DashboardSnapshots.Remove(snapshot);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<DashboardSnapshot> PersistSnapshotAsync(
        Guid empresaId, Guid userId, DateTime fromUtc, DateTime toUtc, string source,
        LeadsStatsResponse stats, CancellationToken ct)
    {
        var snapshot = new DashboardSnapshot
        {
            EmpresaId = empresaId,
            CapturedByUserId = userId,
            CapturedAt = DateTime.UtcNow,
            PeriodFrom = fromUtc,
            PeriodTo = toUtc,
            Source = source,
            PayloadJson = JsonSerializer.Serialize(stats, JsonOpts),
        };
        db.DashboardSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);

        // Recarrega o nome do usuário pra responder com o DTO completo.
        await db.Entry(snapshot).Reference(s => s.CapturedBy).LoadAsync(ct);
        return snapshot;
    }

    private static (DateTime fromUtc, DateTime toUtc, DateTime? prevFromUtc, DateTime? prevToUtc) ResolvePeriod(
        DateTime? from, DateTime? to, DateTime? prevFrom, DateTime? prevTo)
    {
        var fromUtc = from.HasValue ? LeadsQueries.ToUtc(from.Value) : DateTime.UtcNow.AddYears(-100);
        var toUtc = to.HasValue ? LeadsQueries.ToUtc(to.Value) : DateTime.UtcNow.AddDays(1);
        var prevFromUtc = prevFrom.HasValue ? LeadsQueries.ToUtc(prevFrom.Value) : (DateTime?)null;
        var prevToUtc = prevTo.HasValue ? LeadsQueries.ToUtc(prevTo.Value) : (DateTime?)null;
        return (fromUtc, toUtc, prevFromUtc, prevToUtc);
    }

    private static DashboardSnapshotDto ToDto(DashboardSnapshot s, LeadsStatsResponse stats) => new(
        s.Id,
        s.EmpresaId,
        s.CapturedByUserId,
        s.CapturedBy?.Name ?? string.Empty,
        s.CapturedAt,
        s.PeriodFrom,
        s.PeriodTo,
        s.Source,
        stats);
}
