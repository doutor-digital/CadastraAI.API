using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("recebimentos")]
public class Recebimento
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal ValorRecebimento { get; set; }

    /// <summary>"Pix" | "Cartão Crédito" | "Cartão Débito" | "Dinheiro" | "Boleto"</summary>
    [Required, MaxLength(50)]
    public string FormaPagamento { get; set; } = string.Empty;

    [Required]
    public DateTime DataRecebimento { get; set; }

    // FK opcional para Consulta (máx. 2 recebimentos)
    public Guid? ConsultaId { get; set; }
    [ForeignKey(nameof(ConsultaId))]
    public Consulta? Consulta { get; set; }

    // FK opcional para Tratamento (máx. 6 recebimentos)
    public Guid? TratamentoId { get; set; }
    [ForeignKey(nameof(TratamentoId))]
    public Tratamento? Tratamento { get; set; }

    /// <summary>Usuário que registrou o recebimento (null para registros antigos).</summary>
    public Guid? CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}