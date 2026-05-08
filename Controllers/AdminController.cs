using CadastraAI.API.Admin;
using CadastraAI.API.Data;
using CadastraAI.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

/// <summary>
/// Endpoints administrativos protegidos por <see cref="AdminAuthMiddleware"/> (Basic Auth).
/// Não usa [Authorize] porque o gate Basic Auth é aplicado antes da pipeline MVC.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController(AppDbContext db, IRequestLogStore logStore) : ControllerBase
{
    [HttpGet("logs")]
    public ActionResult<IReadOnlyList<RequestLogEntry>> GetLogs(
        [FromQuery] int max = 200,
        [FromQuery] string? method = null,
        [FromQuery] string? path = null,
        [FromQuery] int? minStatus = null)
    {
        return Ok(logStore.Recent(max, method, path, minStatus));
    }

    [HttpDelete("logs")]
    public IActionResult ClearLogs()
    {
        logStore.Clear();
        return NoContent();
    }

    [HttpGet("diagnostic/empresas")]
    public async Task<ActionResult<List<object>>> Empresas(CancellationToken ct)
    {
        var rows = await db.Empresas.AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.Nome,
                e.Tipo,
                e.OwnerUserId,
                OwnerEmail = db.Users.Where(u => u.Id == e.OwnerUserId).Select(u => u.Email).FirstOrDefault(),
                Members = db.Memberships.Count(m => m.EmpresaId == e.Id),
                Leads = db.Leads.Count(l => l.EmpresaId == e.Id),
                LastLeadAt = db.Leads.Where(l => l.EmpresaId == e.Id)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => (DateTime?)l.CreatedAt)
                    .FirstOrDefault(),
                e.CreatedAt,
            })
            .OrderByDescending(x => x.LastLeadAt)
            .ToListAsync(ct);

        return Ok(rows.Cast<object>().ToList());
    }

    [HttpGet("diagnostic/empresas/{empresaId:guid}/leads")]
    public async Task<ActionResult<object>> LeadsByEmpresa(
        Guid empresaId,
        [FromQuery] int recent = 50,
        CancellationToken ct = default)
    {
        recent = Math.Clamp(recent, 1, 500);

        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.Id, e.Nome, e.OwnerUserId })
            .FirstOrDefaultAsync(ct);

        if (empresa is null) return NotFound();

        var total = await db.Leads.CountAsync(l => l.EmpresaId == empresaId, ct);

        var leads = await db.Leads.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(recent)
            .Select(l => new
            {
                l.Id,
                l.Nome,
                l.Telefone,
                l.Origem,
                l.Tipo,
                l.NomeResponsavel,
                l.CreatedAt,
                l.Importado,
                l.AgendouConsulta,
                HasConsulta = l.Consulta != null,
            })
            .ToListAsync(ct);

        return Ok(new { empresa, total, leads });
    }

    [HttpGet("diagnostic/empresas/{empresaId:guid}/members")]
    public async Task<ActionResult<List<object>>> MembersByEmpresa(Guid empresaId, CancellationToken ct)
    {
        var members = await db.Memberships.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User.Name)
            .Select(m => new
            {
                m.UserId,
                m.User.Email,
                m.User.Name,
                m.Role,
                m.CreatedAt,
                m.User.LastLoginAt,
            })
            .ToListAsync(ct);

        return Ok(members.Cast<object>().ToList());
    }

    [HttpGet("diagnostic/users/by-email")]
    public async Task<ActionResult<object>> UserByEmail([FromQuery] string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "email é obrigatório" });

        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(u => new { u.Id, u.Email, u.Name, u.CreatedAt, u.LastLoginAt, u.GoogleSubject })
            .FirstOrDefaultAsync(ct);

        if (user is null) return NotFound(new { message = "usuário não encontrado" });

        var memberships = await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Include(m => m.Empresa)
            .Select(m => new { m.EmpresaId, EmpresaNome = m.Empresa.Nome, m.Role, m.CreatedAt })
            .ToListAsync(ct);

        var leadsCriados = await db.Leads.AsNoTracking()
            .Where(l => db.Memberships.Any(m => m.UserId == user.Id && m.EmpresaId == l.EmpresaId))
            .GroupBy(l => l.EmpresaId)
            .Select(g => new { EmpresaId = g.Key, Total = g.Count(), Ultimo = g.Max(l => l.CreatedAt) })
            .ToListAsync(ct);

        return Ok(new { user, memberships, leadsPorEmpresa = leadsCriados });
    }

    [HttpGet("")]
    [HttpGet("/api/admin/")]
    public ContentResult Index()
    {
        return new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            Content = Html,
        };
    }

    private const string Html = """
<!doctype html>
<html lang="pt-br">
<head>
  <meta charset="utf-8" />
  <title>cadastra.ai · admin</title>
  <style>
    :root { color-scheme: dark; }
    * { box-sizing: border-box; }
    body { font: 13px/1.4 -apple-system, system-ui, "Segoe UI", sans-serif; background: #0d1117; color: #e6edf3; margin: 0; padding: 16px; }
    h1 { margin: 0 0 12px; font-size: 18px; }
    nav { display: flex; gap: 8px; margin-bottom: 12px; flex-wrap: wrap; }
    button, .btn { background: #21262d; color: #e6edf3; border: 1px solid #30363d; border-radius: 6px; padding: 6px 10px; cursor: pointer; font: inherit; }
    button:hover, .btn:hover { background: #30363d; }
    input { background: #0d1117; color: #e6edf3; border: 1px solid #30363d; border-radius: 6px; padding: 6px 10px; font: inherit; }
    table { border-collapse: collapse; width: 100%; font-size: 12px; }
    th, td { border-bottom: 1px solid #21262d; padding: 6px 8px; text-align: left; vertical-align: top; }
    th { background: #161b22; position: sticky; top: 0; }
    tr:hover { background: #161b22; }
    .status-2 { color: #3fb950; }
    .status-3 { color: #d29922; }
    .status-4 { color: #f85149; }
    .status-5 { color: #ff7b72; font-weight: bold; }
    .muted { color: #7d8590; }
    pre { background: #010409; border: 1px solid #21262d; border-radius: 6px; padding: 8px; overflow: auto; max-height: 200px; margin: 4px 0 0; white-space: pre-wrap; word-break: break-all; }
    details summary { cursor: pointer; }
    .row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; margin-bottom: 12px; }
    .pill { display: inline-block; padding: 2px 6px; border-radius: 4px; background: #161b22; border: 1px solid #30363d; font-size: 11px; }
  </style>
</head>
<body>
  <h1>cadastra.ai · admin</h1>
  <nav>
    <button onclick="show('logs')">Logs</button>
    <button onclick="show('empresas')">Empresas</button>
    <button onclick="show('user')">Buscar usuário</button>
    <button onclick="loadCurrent()">Atualizar</button>
  </nav>
  <div id="filters"></div>
  <div id="content">Carregando…</div>

<script>
const $ = (sel) => document.querySelector(sel);
let currentView = 'logs';

function show(view) {
  currentView = view;
  $('#filters').innerHTML = filtersFor(view);
  loadCurrent();
}

function filtersFor(view) {
  if (view === 'logs') {
    return `
      <div class="row">
        <input id="f-method" placeholder="método (POST, GET...)" />
        <input id="f-path" placeholder="filtrar path (ex: leads)" />
        <input id="f-minstatus" type="number" placeholder="status mínimo (400)" />
        <input id="f-max" type="number" value="200" placeholder="máximo" />
        <button onclick="loadCurrent()">Filtrar</button>
        <button onclick="clearLogs()">Limpar logs</button>
      </div>`;
  }
  if (view === 'user') {
    return `
      <div class="row">
        <input id="f-email" placeholder="email do usuário" style="min-width:280px" />
        <button onclick="loadCurrent()">Buscar</button>
      </div>`;
  }
  return '';
}

async function loadCurrent() {
  const c = $('#content');
  c.textContent = 'Carregando…';
  try {
    if (currentView === 'logs') {
      const params = new URLSearchParams();
      const m = $('#f-method')?.value?.trim(); if (m) params.set('method', m);
      const p = $('#f-path')?.value?.trim(); if (p) params.set('path', p);
      const s = $('#f-minstatus')?.value?.trim(); if (s) params.set('minStatus', s);
      const max = $('#f-max')?.value?.trim(); if (max) params.set('max', max);
      const r = await fetch('/api/admin/logs?' + params).then(r => r.json());
      c.innerHTML = renderLogs(r);
    } else if (currentView === 'empresas') {
      const r = await fetch('/api/admin/diagnostic/empresas').then(r => r.json());
      c.innerHTML = renderEmpresas(r);
    } else if (currentView === 'user') {
      const email = $('#f-email')?.value?.trim();
      if (!email) { c.textContent = 'Digite um email.'; return; }
      const res = await fetch('/api/admin/diagnostic/users/by-email?email=' + encodeURIComponent(email));
      if (!res.ok) { c.textContent = (await res.text()) || 'Usuário não encontrado.'; return; }
      c.innerHTML = renderUser(await res.json());
    }
  } catch (e) {
    c.textContent = 'Erro: ' + e.message;
  }
}

function renderLogs(rows) {
  if (!rows.length) return '<div class="muted">Sem requisições registradas.</div>';
  const head = `<tr><th>Quando (UTC)</th><th>Método</th><th>Path</th><th>Status</th><th>ms</th><th>Usuário</th><th>IP</th><th>Body</th></tr>`;
  const body = rows.map(r => `
    <tr>
      <td>${r.at}</td>
      <td>${r.method}</td>
      <td>${r.path}${r.query ? '<span class="muted">' + escapeHtml(r.query) + '</span>' : ''}</td>
      <td class="status-${Math.floor(r.status/100)}">${r.status}</td>
      <td>${r.durationMs}</td>
      <td>${r.userEmail || r.userId || '<span class="muted">anon</span>'}</td>
      <td class="muted">${r.ip || ''}</td>
      <td>${r.requestBody ? `<details><summary>ver</summary><pre>${escapeHtml(r.requestBody)}</pre></details>` : ''}${r.errorMessage ? `<pre style="color:#f85149">${escapeHtml(r.errorMessage)}</pre>` : ''}</td>
    </tr>`).join('');
  return `<table><thead>${head}</thead><tbody>${body}</tbody></table>`;
}

function renderEmpresas(rows) {
  if (!rows.length) return '<div class="muted">Nenhuma empresa.</div>';
  const head = `<tr><th>Empresa</th><th>Owner</th><th>Membros</th><th>Leads</th><th>Último lead</th><th>EmpresaId</th></tr>`;
  const body = rows.map(e => `
    <tr>
      <td>${escapeHtml(e.nome)}</td>
      <td>${escapeHtml(e.ownerEmail || '')}</td>
      <td>${e.members}</td>
      <td><a href="#" onclick="loadEmpresaLeads('${e.id}'); return false;">${e.leads}</a></td>
      <td>${e.lastLeadAt || '<span class="muted">—</span>'}</td>
      <td><span class="pill">${e.id}</span></td>
    </tr>`).join('');
  return `<table><thead>${head}</thead><tbody>${body}</tbody></table>
    <div id="empresa-leads"></div>`;
}

async function loadEmpresaLeads(id) {
  const r = await fetch('/api/admin/diagnostic/empresas/' + id + '/leads?recent=100').then(r => r.json());
  const target = document.getElementById('empresa-leads');
  if (!r.leads.length) { target.innerHTML = '<div class="muted">Sem leads nessa empresa.</div>'; return; }
  const head = `<tr><th>Criado em</th><th>Nome</th><th>Telefone</th><th>Origem</th><th>Responsável</th><th>Importado</th><th>Agendou</th></tr>`;
  const body = r.leads.map(l => `
    <tr>
      <td>${l.createdAt}</td>
      <td>${escapeHtml(l.nome)}</td>
      <td>${escapeHtml(l.telefone)}</td>
      <td>${escapeHtml(l.origem)}</td>
      <td>${escapeHtml(l.nomeResponsavel)}</td>
      <td>${l.importado ? 'sim' : 'não'}</td>
      <td>${l.agendouConsulta ? 'sim' : 'não'}</td>
    </tr>`).join('');
  target.innerHTML = `<h3>Leads da empresa ${escapeHtml(r.empresa.nome)} (mostrando ${r.leads.length} de ${r.total})</h3>
    <table><thead>${head}</thead><tbody>${body}</tbody></table>`;
}

function renderUser(d) {
  const u = d.user;
  const m = (d.memberships || []).map(x => `<li><span class="pill">${x.role}</span> ${escapeHtml(x.empresaNome)} <span class="muted">(${x.empresaId})</span></li>`).join('');
  const lp = (d.leadsPorEmpresa || []).map(x => `<li>${x.total} leads em <span class="pill">${x.empresaId}</span> · último: ${x.ultimo}</li>`).join('');
  return `
    <h3>${escapeHtml(u.name)} <span class="muted">${escapeHtml(u.email)}</span></h3>
    <p>userId: <span class="pill">${u.id}</span> · último login: ${u.lastLoginAt || '—'}</p>
    <h4>Memberships</h4>
    <ul>${m || '<li class="muted">nenhum</li>'}</ul>
    <h4>Leads por empresa que ele acessa</h4>
    <ul>${lp || '<li class="muted">nenhum</li>'}</ul>`;
}

async function clearLogs() {
  if (!confirm('Limpar todos os logs em memória?')) return;
  await fetch('/api/admin/logs', { method: 'DELETE' });
  loadCurrent();
}

function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c]);
}

show('logs');
</script>
</body>
</html>
""";
}
