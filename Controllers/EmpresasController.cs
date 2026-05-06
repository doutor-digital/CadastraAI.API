using CadastraAI.API.Auth;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Email;
using CadastraAI.API.Models;
using CadastraAI.API.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Authorize]
[Route("api/empresas")]
public class EmpresasController(
    AppDbContext db,
    IConfiguration config,
    IEmailSender emailSender,
    ILocalFileStorage fileStorage,
    ILogger<EmpresasController> logger) : ControllerBase
{
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private string AppUrl => config["AppUrl"] ?? "http://localhost:3000";

    private async Task<Membership?> RequireMembership(Guid empresaId, Guid userId, CancellationToken ct) =>
        await db.Memberships.FirstOrDefaultAsync(m => m.EmpresaId == empresaId && m.UserId == userId, ct);

    [HttpGet]
    public async Task<ActionResult<List<EmpresaDto>>> List(CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var rows = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Include(m => m.Empresa).ThenInclude(e => e.Owner)
            .Select(m => new
            {
                m.Empresa,
                MemberCount = db.Memberships.Count(mm => mm.EmpresaId == m.EmpresaId),
                m.Role,
            })
            .ToListAsync(ct);

        var result = rows.Select(r => new EmpresaDto(
            r.Empresa.Id,
            r.Empresa.Nome,
            r.Empresa.Tipo,
            r.Empresa.LogoUrl,
            r.Empresa.OwnerUserId,
            r.Empresa.Owner.Name,
            r.Empresa.CreatedAt,
            r.MemberCount,
            r.Role)).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EmpresaDto>> Create([FromBody] CreateEmpresaRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var owner = await db.Users.FindAsync([userId.Value], ct);
        if (owner is null) return Unauthorized();

        var empresa = new Empresa
        {
            Nome = req.Nome.Trim(),
            Tipo = req.Tipo?.Trim(),
            OwnerUserId = owner.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Empresas.Add(empresa);

        db.Memberships.Add(new Membership
        {
            EmpresaId = empresa.Id,
            UserId = owner.Id,
            Role = MembershipRole.Owner,
            CreatedAt = DateTime.UtcNow,
        });

        foreach (var (nome, cor) in MotivoNaoFechamento.Defaults)
        {
            db.MotivosNaoFechamento.Add(new MotivoNaoFechamento
            {
                EmpresaId = empresa.Id,
                Nome = nome,
                Cor = cor,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new EmpresaDto(
            empresa.Id, empresa.Nome, empresa.Tipo, empresa.LogoUrl, owner.Id, owner.Name,
            empresa.CreatedAt, MemberCount: 1, MyRole: MembershipRole.Owner));
    }

    [HttpPost("{empresaId:guid}/logo")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<EmpresaDto>> UploadLogo(Guid empresaId, IFormFile file, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        var me = await RequireMembership(empresaId, userId.Value, ct);
        if (me is null) return Forbid();
        if (me.Role != MembershipRole.Owner && me.Role != MembershipRole.Admin)
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Nenhum arquivo enviado." });
        }

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return NotFound();

        string newUrl;
        try
        {
            newUrl = await fileStorage.SaveAsync(file, "empresa-logos", ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var previous = empresa.LogoUrl;
        empresa.LogoUrl = newUrl;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previous))
        {
            fileStorage.TryDelete(previous);
        }

        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Id == empresa.OwnerUserId, ct);
        var memberCount = await db.Memberships.CountAsync(m => m.EmpresaId == empresaId, ct);

        return Ok(new EmpresaDto(
            empresa.Id, empresa.Nome, empresa.Tipo, empresa.LogoUrl, owner.Id, owner.Name,
            empresa.CreatedAt, memberCount, me.Role));
    }

    [HttpDelete("{empresaId:guid}/logo")]
    public async Task<IActionResult> DeleteLogo(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        var me = await RequireMembership(empresaId, userId.Value, ct);
        if (me is null) return Forbid();
        if (me.Role != MembershipRole.Owner && me.Role != MembershipRole.Admin) return Forbid();

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return NotFound();

        var previous = empresa.LogoUrl;
        empresa.LogoUrl = null;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previous))
        {
            fileStorage.TryDelete(previous);
        }
        return NoContent();
    }

    [HttpGet("{empresaId:guid}/members")]
    public async Task<ActionResult<List<MemberDto>>> Members(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await RequireMembership(empresaId, userId.Value, ct) is null) return Forbid();

        var members = await db.Memberships
            .AsNoTracking()
            .Where(m => m.EmpresaId == empresaId)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User.Name)
            .Select(m => new MemberDto(
                m.UserId, m.User.Email, m.User.Name, m.User.AvatarUrl, m.Role, m.CreatedAt))
            .ToListAsync(ct);

        return Ok(members);
    }

    [HttpDelete("{empresaId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid empresaId, Guid memberUserId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        var me = await RequireMembership(empresaId, userId.Value, ct);
        if (me is null) return Forbid();
        if (me.Role != MembershipRole.Owner && me.Role != MembershipRole.Admin)
        {
            return Forbid();
        }

        var target = await db.Memberships.FirstOrDefaultAsync(m => m.EmpresaId == empresaId && m.UserId == memberUserId, ct);
        if (target is null) return NotFound();
        if (target.Role == MembershipRole.Owner)
        {
            return BadRequest(new { message = "O dono da empresa não pode ser removido." });
        }

        db.Memberships.Remove(target);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{empresaId:guid}/invites")]
    public async Task<ActionResult<List<InviteDto>>> ListInvites(Guid empresaId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        if (await RequireMembership(empresaId, userId.Value, ct) is null) return Forbid();

        var rows = await db.Invites
            .AsNoTracking()
            .Where(i => i.EmpresaId == empresaId)
            .Include(i => i.Empresa)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return Ok(rows.Select(i => MapInvite(i)).ToList());
    }

    [HttpPost("{empresaId:guid}/invites")]
    public async Task<ActionResult<InviteDto>> CreateInvite(Guid empresaId, [FromBody] CreateInviteRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        var me = await RequireMembership(empresaId, userId.Value, ct);
        if (me is null) return Forbid();
        if (me.Role != MembershipRole.Owner && me.Role != MembershipRole.Admin)
        {
            return Forbid();
        }

        var email = Normalize(req.Email);

        // If an active user already exists for that email and is already a member, refuse.
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existingUser is not null)
        {
            var alreadyMember = await db.Memberships.AnyAsync(
                m => m.EmpresaId == empresaId && m.UserId == existingUser.Id, ct);
            if (alreadyMember)
            {
                return Conflict(new { message = "Este usuário já é membro da empresa." });
            }
        }

        // Re-use a pending invite if one exists for the same email.
        var pending = await db.Invites.FirstOrDefaultAsync(
            i => i.EmpresaId == empresaId && i.Email == email && i.Status == InviteStatus.Pending, ct);

        Invite invite;
        if (pending is not null)
        {
            pending.Role = req.Role;
            pending.ExpiresAt = DateTime.UtcNow.AddDays(7);
            invite = pending;
        }
        else
        {
            invite = new Invite
            {
                EmpresaId = empresaId,
                Email = email,
                Role = req.Role,
                Token = InviteTokenGenerator.Generate(),
                Status = InviteStatus.Pending,
                InvitedByUserId = userId.Value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };
            db.Invites.Add(invite);
        }

        await db.SaveChangesAsync(ct);

        var withEmpresa = await db.Invites
            .AsNoTracking()
            .Include(i => i.Empresa)
            .FirstAsync(i => i.Id == invite.Id, ct);

        var baseDto = MapInvite(withEmpresa);

        // Dispatch email — best-effort. The invite URL is always returned in the response so the
        // inviter can copy/share it manually if email delivery fails (unverified domain, etc.).
        bool? delivered = null;
        string? deliveryError = null;
        try
        {
            var inviterName = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(ct) ?? "Sua empresa";

            var message = InviteEmailComposer.Compose(
                to: invite.Email,
                empresaNome: withEmpresa.Empresa.Nome,
                inviterName: inviterName,
                role: invite.Role,
                inviteUrl: baseDto.InviteUrl,
                expiresAt: invite.ExpiresAt);

            var result = await emailSender.SendAsync(message, ct);
            delivered = result.Delivered;
            deliveryError = result.ErrorMessage;
            if (!result.Delivered)
            {
                logger.LogWarning(
                    "Invite email for {Email} not delivered: {Error}",
                    invite.Email, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error dispatching invite email to {Email}", invite.Email);
            delivered = false;
            deliveryError = $"Erro inesperado: {ex.Message}";
        }

        return Ok(baseDto with { EmailDelivered = delivered, EmailError = deliveryError });
    }

    [HttpDelete("{empresaId:guid}/invites/{inviteId:guid}")]
    public async Task<IActionResult> RevokeInvite(Guid empresaId, Guid inviteId, CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();
        var me = await RequireMembership(empresaId, userId.Value, ct);
        if (me is null) return Forbid();
        if (me.Role != MembershipRole.Owner && me.Role != MembershipRole.Admin) return Forbid();

        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Id == inviteId && i.EmpresaId == empresaId, ct);
        if (invite is null) return NotFound();

        invite.Status = InviteStatus.Revoked;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private InviteDto MapInvite(Invite i) => new(
        i.Id,
        i.EmpresaId,
        i.Empresa.Nome,
        i.Email,
        i.Role,
        i.Token,
        $"{AppUrl.TrimEnd('/')}/aceitar-convite/{i.Token}",
        i.Status,
        i.CreatedAt,
        i.ExpiresAt,
        i.AcceptedAt);
}
