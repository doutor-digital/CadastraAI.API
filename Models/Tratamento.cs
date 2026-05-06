using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("tratamentos")]
public class Tratamento
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK para Consulta (1:1)
    [Required]
    public Guid ConsultaId { get; set; }

    [ForeignKey(nameof(ConsultaId))]
    public Consulta Consulta { get; set; } = null!;

    /// <summary>
    /// "tratamento_pontual" | "clinico_mensal" | "clinico_semestral" |
    /// "essencial_mensal" | "essencial_semestral" | "essencial_anual"
    /// </summary>
    [Required, MaxLength(100)]
    public string PlanoTratamento { get; set; } = string.Empty;

    /// <summary>"mensal" | "semestral" | "anual" | null</summary>
    [MaxLength(50)]
    public string? PlanoPilates { get; set; }

    /// <summary>"3x_mensal" | "3x_semestral" | "3x_anual" | null</summary>
    [MaxLength(50)]
    public string? Musculacao { get; set; }

    /// <summary>
    /// "liberacao_parcial_individual" | "liberacao_total_individual" |
    /// "sessao_fisioterapia" | null
    /// </summary>
    [MaxLength(100)]
    public string? Procedimento { get; set; }

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal ValorPlano { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation — até 6 recebimentos
    public ICollection<Recebimento> Recebimentos { get; set; } = new List<Recebimento>();
}