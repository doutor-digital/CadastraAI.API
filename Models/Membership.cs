using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

public enum MembershipRole
{
    Owner = 0,
    Admin = 1,
    Member = 2,
}

[Table("memberships")]
public class Membership
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public MembershipRole Role { get; set; } = MembershipRole.Member;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
