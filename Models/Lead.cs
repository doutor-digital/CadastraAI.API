using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("leads")]
public class Lead
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Telefone { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Origem { get; set; } = string.Empty;

    /// <summary>"Cadastro" ou "Resgate"</summary>
    [Required, MaxLength(20)]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>"Mensagem" | "Ligação" | "Disparo em Massa"</summary>
    [MaxLength(50)]
    public string? TipoResgate { get; set; }

    public bool Interacao { get; set; }

    public bool AgendouConsulta { get; set; }

    public bool PagamentoAntecipado { get; set; }

    public DateTime? DataAgendamento { get; set; }

    [MaxLength(200)]
    public string? MotivoNaoAgendamento { get; set; }

    /// <summary>"Rayssa" | "Maria Eduarda" | "Adriele"</summary>
    [Required, MaxLength(100)]
    public string NomeResponsavel { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Consulta? Consulta { get; set; }
}