using CadastraAI.API.Auth;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Route("api/invites")]
public class InvitesController(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwt,
    IGoogleTokenValidator googleValidator) : ControllerBase
{
    private static UserDto ToUserDto(User u) => new(u.Id, u.Email, u.Name, u.AvatarUrl, u.CreatedAt, u.LastLoginAt);
    private AuthResponse Issue(User user) => new(jwt.Issue(user), jwt.ExpirationSeconds, ToUserDto(user));

    private async Task<Invite?> ResolveInvite(string token, CancellationToken ct, bool tracked = false)
    {
        var query = tracked ? db.Invites : db.Invites.AsNoTracking();
        return await query.Include(i => i.Empresa).FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    private static bool IsAcceptable(Invite invite) =>
        invite.Status == InviteStatus.Pending && invite.ExpiresAt > DateTime.UtcNow;

    [HttpGet("{token}")]
    public async Task<ActionResult<InvitePreviewDto>> Preview(string token, CancellationToken ct)
    {
        var invite = await ResolveInvite(token, ct);
        if (invite is null) return NotFound(new { message = "Convite não encontrado." });

        var status = invite.Status;
        if (status == InviteStatus.Pending && invite.ExpiresAt <= DateTime.UtcNow)
        {
            status = InviteStatus.Expired;
        }

        var existingUser = await db.Users.AsNoTracking().AnyAsync(u => u.Email == invite.Email, ct);

        return Ok(new InvitePreviewDto(
            invite.Empresa.Nome,
            invite.Email,
            invite.Role,
            status,
            invite.ExpiresAt,
            existingUser));
    }

    [HttpPost("{token}/accept-password")]
    public async Task<ActionResult<AuthResponse>> AcceptWithPassword(
        string token,
        [FromBody] AcceptInviteWithPasswordRequest req,
        CancellationToken ct)
    {
        var invite = await ResolveInvite(token, ct, tracked: true);
        if (invite is null) return NotFound(new { message = "Convite não encontrado." });
        if (!IsAcceptable(invite)) return BadRequest(new { message = "Convite expirado ou já utilizado." });

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == invite.Email, ct);
        if (user is null)
        {
            user = new User
            {
                Email = invite.Email,
                Name = req.Name.Trim(),
                PasswordHash = passwordHasher.Hash(req.Password),
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
        else if (string.IsNullOrEmpty(user.PasswordHash))
        {
            // The user exists (e.g. via Google) but has never set a password. Use this acceptance to set one.
            user.PasswordHash = passwordHasher.Hash(req.Password);
            user.LastLoginAt = DateTime.UtcNow;
        }
        else
        {
            // User already has a password — they must log in normally; we still accept the invite if password matches.
            if (!passwordHasher.Verify(req.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Já existe uma conta com esse e-mail. Senha incorreta." });
            }
            user.LastLoginAt = DateTime.UtcNow;
        }

        await EnsureMembership(invite, user, ct);

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByUserId = user.Id;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(Issue(user));
    }

    [HttpPost("{token}/accept-google")]
    public async Task<ActionResult<AuthResponse>> AcceptWithGoogle(
        string token,
        [FromBody] AcceptInviteWithGoogleRequest req,
        CancellationToken ct)
    {
        var invite = await ResolveInvite(token, ct, tracked: true);
        if (invite is null) return NotFound(new { message = "Convite não encontrado." });
        if (!IsAcceptable(invite)) return BadRequest(new { message = "Convite expirado ou já utilizado." });

        GoogleIdentity identity;
        try
        {
            identity = await googleValidator.ValidateAsync(req.IdToken, ct);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = $"Token Google inválido: {ex.Message}" });
        }

        if (!string.Equals(identity.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = $"Faça login no Google com o mesmo e-mail do convite ({invite.Email})."
            });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject, ct)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Email == invite.Email, ct);

        if (user is null)
        {
            user = new User
            {
                Email = invite.Email,
                Name = identity.Name,
                GoogleSubject = identity.Subject,
                AvatarUrl = identity.Picture,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            if (string.IsNullOrEmpty(user.GoogleSubject)) user.GoogleSubject = identity.Subject;
            if (string.IsNullOrEmpty(user.AvatarUrl)) user.AvatarUrl = identity.Picture;
            user.LastLoginAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await EnsureMembership(invite, user, ct);

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByUserId = user.Id;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(Issue(user));
    }

    private async Task EnsureMembership(Invite invite, User user, CancellationToken ct)
    {
        var existing = await db.Memberships.FirstOrDefaultAsync(
            m => m.EmpresaId == invite.EmpresaId && m.UserId == user.Id, ct);

        if (existing is null)
        {
            db.Memberships.Add(new Membership
            {
                EmpresaId = invite.EmpresaId,
                UserId = user.Id,
                Role = invite.Role,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }
}
