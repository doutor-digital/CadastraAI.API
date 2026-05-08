using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("leads")]
public class Lead
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

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

    /// <summary>true quando o lead veio via importação em massa; false quando foi cadastrado manualmente.</summary>
    public bool Importado { get; set; }

    /// <summary>
    /// Usuário que criou o registro. Nullable porque registros antigos (anteriores à
    /// migration de auditoria) não têm autor conhecido — o frontend mostra "—" nesses casos.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedBy { get; set; }

    // Navigation
    public Consulta? Consulta { get; set; }
}