using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

/// <summary>
/// Lead vindo do Kommo (via webhook ou sync) aguardando revisão/promoção pelo usuário.
/// O frontend faz o gap-analysis em cima do RawJson; depois que vira lead no nosso sistema,
/// o item fica como Imported guardando o ImportedLeadId pra rastreabilidade.
/// </summary>
[Table("kommo_inbox_items")]
public class KommoInboxItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    /// <summary>ID do lead na Kommo, quando determinável.</summary>
    public long? KommoLeadId { get; set; }

    /// <summary>"webhook" | "sync"</summary>
    [Required, MaxLength(16)]
    public string Source { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Payload bruto do Kommo serializado em JSON (lead + contact merged).</summary>
    [Required, Column(TypeName = "jsonb")]
    public string RawJson { get; set; } = "{}";

    /// <summary>"pending" | "imported" | "discarded"</summary>
    [Required, MaxLength(16)]
    public string Status { get; set; } = "pending";

    /// <summary>Set quando Status="imported" — referência cruzada para o lead criado.</summary>
    public Guid? ImportedLeadId { get; set; }

    [ForeignKey(nameof(ImportedLeadId))]
    public Lead? ImportedLead { get; set; }

    public DateTime? ImportedAt { get; set; }

    public Guid? ImportedByUserId { get; set; }

    [ForeignKey(nameof(ImportedByUserId))]
    public User? ImportedBy { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
