using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

/// <summary>
/// Registro chronológico de eventos de domínio para auditoria. Multi-tenant via EmpresaId.
/// Granularidade do diff: lista de campos que mudaram em updates (sem valores), pra não
/// inflar o storage. Em creates/deletes, ChangedFields fica null.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Usuário autor da ação (null para webhooks/imports automatizados).</summary>
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>Snapshot do nome no momento do evento — sobrevive a renames/deletes do User.</summary>
    [MaxLength(200)]
    public string? UserName { get; set; }

    [MaxLength(254)]
    public string? UserEmail { get; set; }

    /// <summary>
    /// "lead.create" | "lead.update" | "lead.delete" | "lead.bulk_import" |
    /// "consulta.create" | "consulta.update" | "consulta.delete" |
    /// "tratamento.create" | "tratamento.update" | "tratamento.delete" |
    /// "recebimento.create" | "recebimento.delete" |
    /// "kommo.sync" | "kommo.webhook" | "kommo.promote"
    /// </summary>
    [Required, MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    /// <summary>"Lead" | "Consulta" | "Tratamento" | "Recebimento" | "KommoInboxItem"</summary>
    [Required, MaxLength(32)]
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>Rótulo legível (ex.: nome do lead) para sobreviver a deletes.</summary>
    [MaxLength(200)]
    public string? EntityLabel { get; set; }

    /// <summary>
    /// jsonb com {"changed":["Telefone","Origem"]} para updates ou {"count":N} para
    /// imports em massa. Mantido propositalmente leve — sem valores antes/depois.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Meta { get; set; }

    [MaxLength(45)]
    public string? Ip { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}
