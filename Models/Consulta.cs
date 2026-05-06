using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("consultas")]
public class Consulta
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK para Lead (1:1)
    [Required]
    public Guid LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public Lead Lead { get; set; } = null!;

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal ValorConsulta { get; set; }

    public bool PagamentoAntecipado { get; set; }

    [Required, MaxLength(500)]
    public string TratamentoIndicado { get; set; } = string.Empty;

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal Orcamento { get; set; }

    /// <summary>Se o lead compareceu na consulta marcada.</summary>
    public bool Compareceu { get; set; }

    public bool FechouTratamento { get; set; }

    /// <summary>
    /// Semáforos: "fechou_total" | "fechou_parcial" | "assinou_sem_entrada" |
    /// "decide_com_familia" | "verifica_pagamento" | "exame_imagem" |
    /// "mora_fora" | "outra_patologia" | "sem_condicoes"
    /// </summary>
    [MaxLength(200)]
    public string? MotivoNaoFechamento { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Recebimento> Recebimentos { get; set; } = new List<Recebimento>();
    public Tratamento? Tratamento { get; set; }
}