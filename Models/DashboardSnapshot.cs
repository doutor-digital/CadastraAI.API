using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("dashboard_snapshots")]
public class DashboardSnapshot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    [Required]
    public Guid CapturedByUserId { get; set; }

    [ForeignKey(nameof(CapturedByUserId))]
    public User CapturedBy { get; set; } = null!;

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PeriodFrom { get; set; }

    public DateTime? PeriodTo { get; set; }

    /// <summary>"sync" ou "manual" — origem do snapshot.</summary>
    [Required, MaxLength(16)]
    public string Source { get; set; } = "manual";

    /// <summary>JSON serializado do LeadsStatsResponse capturado no momento.</summary>
    [Required, Column(TypeName = "jsonb")]
    public string PayloadJson { get; set; } = "{}";
}
