using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("users")]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>BCrypt hash; null when the user only authenticates via Google.</summary>
    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    /// <summary>Google "sub" claim — stable per Google account. Null until the user signs in with Google.</summary>
    [MaxLength(64)]
    public string? GoogleSubject { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
