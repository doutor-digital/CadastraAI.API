using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("empresas")]
public class Empresa
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Optional descriptor (clínica, consultório, instituto…)</summary>
    [MaxLength(100)]
    public string? Tipo { get; set; }

    /// <summary>
    /// Public-facing URL of the empresa's logo. Stored on the API filesystem under
    /// wwwroot/uploads/empresa-logos/. Will move to a bucket later — keep as a plain URL string.
    /// </summary>
    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    /// <summary>Owner user — created the empresa, cannot be removed via the members endpoint.</summary>
    [Required]
    public Guid OwnerUserId { get; set; }

    [ForeignKey(nameof(OwnerUserId))]
    public User Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public ICollection<Invite> Invites { get; set; } = new List<Invite>();
}
