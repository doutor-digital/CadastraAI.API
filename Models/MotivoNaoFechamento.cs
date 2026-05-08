using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

[Table("motivos_nao_fechamento")]
public class MotivoNaoFechamento
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Cor do semáforo: "verde" (fechou), "amarelo" (pendente), "vermelho" (perdido).</summary>
    [Required, MaxLength(20)]
    public string Cor { get; set; } = "amarelo";

    /// <summary>Motivos default semeados quando a empresa é criada — não podem ser deletados.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static readonly (string Nome, string Cor)[] Defaults =
    [
        ("Fechou tratamento (parcial/total)", "verde"),
        ("Assinou contrato, sem entrada", "amarelo"),
        ("Vai decidir com familiares", "amarelo"),
        ("Vai verificar a melhor forma de pagamento", "amarelo"),
        ("Solicitado exame de imagem", "amarelo"),
        ("Mora fora +50km", "vermelho"),
        ("Outra patologia", "vermelho"),
        ("Sem condições financeiras", "vermelho"),
    ];

    public static readonly HashSet<string> CoresValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "verde", "amarelo", "vermelho",
    };
}
