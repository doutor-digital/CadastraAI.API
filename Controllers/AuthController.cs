using CadastraAI.API.Auth;
using CadastraAI.API.Data;
using CadastraAI.API.Dtos;
using CadastraAI.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwt,
    IGoogleTokenValidator googleValidator) : ControllerBase
{
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static UserDto ToDto(User u) => new(
        u.Id, u.Email, u.Name, u.AvatarUrl, u.CreatedAt, u.LastLoginAt);

    private AuthResponse Issue(User user) => new(jwt.Issue(user), jwt.ExpirationSeconds, ToDto(user));

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var email = Normalize(req.Email);

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
        {
            return Conflict(new { message = "Já existe uma conta com esse e-mail." });
        }

        var user = new User
        {
            Email = email,
            Name = req.Name.Trim(),
            PasswordHash = passwordHasher.Hash(req.Password),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Ok(Issue(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var email = Normalize(req.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        if (!passwordHasher.Verify(req.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(Issue(user));
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        GoogleIdentity identity;
        try
        {
            identity = await googleValidator.ValidateAsync(req.IdToken, ct);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = $"Token Google inválido: {ex.Message}" });
        }

        // Match by Google subject first, then by email (link existing email/password account to Google).
        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject, ct)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email, ct);

        if (user is null)
        {
            user = new User
            {
                Email = identity.Email,
                Name = identity.Name,
                GoogleSubject = identity.Subject,
                AvatarUrl = identity.Picture,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else if (string.IsNullOrEmpty(user.GoogleSubject))
        {
            user.GoogleSubject = identity.Subject;
            if (string.IsNullOrEmpty(user.AvatarUrl)) user.AvatarUrl = identity.Picture;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(Issue(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Unauthorized();

        return Ok(ToDto(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // JWT is stateless — the client just discards the token. Endpoint kept for symmetry.
        return NoContent();
    }
}
