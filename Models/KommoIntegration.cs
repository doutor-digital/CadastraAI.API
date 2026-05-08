using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CadastraAI.API.Models;

/// <summary>
/// Configuração da integração Kommo por empresa. Token armazenado criptografado
/// (IDataProtector) — nunca expor o token cru em DTOs ou logs.
/// </summary>
[Table("kommo_integrations")]
public class KommoIntegration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EmpresaId { get; set; }

    [ForeignKey(nameof(EmpresaId))]
    public Empresa Empresa { get; set; } = null!;

    [Required, MaxLength(80)]
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>Long-lived access token, criptografado at-rest via IDataProtector.</summary>
    [Required]
    public string AccessTokenEncrypted { get; set; } = string.Empty;

    /// <summary>Últimos 4 caracteres do token cru — pra exibir "••••XXXX" sem precisar descriptografar.</summary>
    [MaxLength(4)]
    public string? TokenSuffix { get; set; }

    /// <summary>Segredo opcional para validar webhooks (Kommo manda no querystring).</summary>
    [MaxLength(120)]
    public string? WebhookSecret { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedBy { get; set; }
}
