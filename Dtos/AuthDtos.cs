using System.ComponentModel.DataAnnotations;

namespace CadastraAI.API.Dtos;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(200)] string Name);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record GoogleLoginRequest(
    [Required] string IdToken);

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string? AvatarUrl,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record AuthResponse(
    string Token,
    int ExpiresIn,
    UserDto User);
