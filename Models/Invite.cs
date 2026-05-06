using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

public enum InviteStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}

[Table("invites")]
public class Invite
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public MembershipRole Role { get; set; } = MembershipRole.Member;

    /// <summary>Opaque random token used in the invite URL.</summary>
    [Required, MaxLength(64)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public InviteStatus Status { get; set; } = InviteStatus.Pending;

    [Required]
    public Guid InvitedByUserId { get; set; }

    [ForeignKey(nameof(InvitedByUserId))]
    public User InvitedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public DateTime? AcceptedAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }
}
