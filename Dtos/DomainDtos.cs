using System.ComponentModel.DataAnnotations;

namespace CadastraAI.API.Dtos;

// ========== Lead ==========

public record CreateLeadRequest(
    [Required, MaxLength(200)] string Nome,
    [Required, MaxLength(20)] string Telefone,
    [Required, MaxLength(100)] string Origem,
    [Required, MaxLength(20)] string Tipo,
    [MaxLength(50)] string? TipoResgate,
    bool Interacao,
    bool AgendouConsulta,
    bool PagamentoAntecipado,
    DateTime? DataAgendamento,
    [MaxLength(200)] string? MotivoNaoAgendamento,
    [Required, MaxLength(100)] string NomeResponsavel,
    DateTime? CreatedAt = null);

public record UpdateLeadRequest(
    [MaxLength(200)] string? Nome,
    [MaxLength(20)] string? Telefone,
    [MaxLength(100)] string? Origem,
    [MaxLength(20)] string? Tipo,
    [MaxLength(50)] string? TipoResgate,
    bool? Interacao,
    bool? AgendouConsulta,
    bool? PagamentoAntecipado,
    DateTime? DataAgendamento,
    [MaxLength(200)] string? MotivoNaoAgendamento,
    [MaxLength(100)] string? NomeResponsavel);

public record PaginatedLeadsResponse(
    List<LeadSummaryDto> Items,
    int Total,
    int Page,
    int PageSize);

public record LeadsStatsPeriod(
    int Leads,
    int Agendados,
    int ComConsulta,
    int Compareceram,
    int Fecharam,
    int Cadastros,
    int Resgates,
    int LeadsManuais,
    int LeadsImportados);

public record OrigemBucket(string Nome, int Count);

public record ResponsavelBucket(string Nome, int Leads, int Compareceram, int Fecharam);

public record LeadsStatsResponse(
    LeadsStatsPeriod Current,
    LeadsStatsPeriod? Previous,
    List<OrigemBucket> TopOrigens,
    List<ResponsavelBucket> TopResponsaveis);

public record LeadSummaryDto(
    Guid Id,
    Guid EmpresaId,
    string Nome,
    string Telefone,
    string Origem,
    string Tipo,
    string? TipoResgate,
    bool Interacao,
    bool AgendouConsulta,
    bool PagamentoAntecipado,
    DateTime? DataAgendamento,
    string? MotivoNaoAgendamento,
    string NomeResponsavel,
    DateTime CreatedAt,
    bool Importado,
    Guid? CreatedByUserId,
    string? CreatedByName,
    bool TemConsulta,
    bool? Compareceu,
    bool? FechouTratamento,
    string? MotivoNaoFechamento,
    Guid? ConsultaId,
    bool TemTratamento,
    decimal? ConsultaOrcamento);

public record LeadDetailDto(
    Guid Id,
    Guid EmpresaId,
    string Nome,
    string Telefone,
    string Origem,
    string Tipo,
    string? TipoResgate,
    bool Interacao,
    bool AgendouConsulta,
    bool PagamentoAntecipado,
    DateTime? DataAgendamento,
    string? MotivoNaoAgendamento,
    string NomeResponsavel,
    DateTime CreatedAt,
    bool Importado,
    Guid? CreatedByUserId,
    string? CreatedByName,
    ConsultaDto? Consulta);

public record BulkCreateLeadsRequest(
    [Required] List<CreateLeadRequest> Leads);

public record BulkLeadErrorDto(int Index, string Error);

public record BulkCreateLeadsResponse(
    int TotalReceived,
    int CreatedCount,
    int FailedCount,
    List<LeadDetailDto> Created,
    List<BulkLeadErrorDto> Failed);

// ========== Consulta ==========

public record CreateConsultaRequest(
    [Range(0, 999999.99)] decimal ValorConsulta,
    bool PagamentoAntecipado,
    [Required, MaxLength(500)] string TratamentoIndicado,
    [Range(0, 9999999.99)] decimal Orcamento,
    bool Compareceu,
    bool FechouTratamento,
    [MaxLength(200)] string? MotivoNaoFechamento);

public record UpdateConsultaRequest(
    decimal? ValorConsulta,
    bool? PagamentoAntecipado,
    [MaxLength(500)] string? TratamentoIndicado,
    decimal? Orcamento,
    bool? Compareceu,
    bool? FechouTratamento,
    [MaxLength(200)] string? MotivoNaoFechamento);

public record ConsultaDto(
    Guid Id,
    Guid LeadId,
    decimal ValorConsulta,
    bool PagamentoAntecipado,
    string TratamentoIndicado,
    decimal Orcamento,
    bool Compareceu,
    bool FechouTratamento,
    string? MotivoNaoFechamento,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    string? CreatedByName,
    TratamentoDto? Tratamento,
    List<RecebimentoDto> Recebimentos);

// ========== Tratamento ==========

public record CreateTratamentoRequest(
    [Required, MaxLength(100)] string PlanoTratamento,
    [MaxLength(50)] string? PlanoPilates,
    [MaxLength(50)] string? Musculacao,
    [MaxLength(100)] string? Procedimento,
    [Range(0, 9999999.99)] decimal ValorPlano);

public record UpdateTratamentoRequest(
    [MaxLength(100)] string? PlanoTratamento,
    [MaxLength(50)] string? PlanoPilates,
    [MaxLength(50)] string? Musculacao,
    [MaxLength(100)] string? Procedimento,
    decimal? ValorPlano);

public record TratamentoDto(
    Guid Id,
    Guid ConsultaId,
    string PlanoTratamento,
    string? PlanoPilates,
    string? Musculacao,
    string? Procedimento,
    decimal ValorPlano,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    string? CreatedByName,
    List<RecebimentoDto> Recebimentos);

// ========== Recebimento ==========

public record CreateRecebimentoRequest(
    [Range(0.01, 9999999.99)] decimal ValorRecebimento,
    [Required, MaxLength(50)] string FormaPagamento,
    [Required] DateTime DataRecebimento);

public record RecebimentoDto(
    Guid Id,
    Guid? ConsultaId,
    Guid? TratamentoId,
    decimal ValorRecebimento,
    string FormaPagamento,
    DateTime DataRecebimento,
    Guid? CreatedByUserId,
    string? CreatedByName,
    DateTime? CreatedAt);

// ========== Dashboard ==========

public record DashboardSyncRequest(
    DateTime? From,
    DateTime? To,
    DateTime? PrevFrom,
    DateTime? PrevTo,
    string? Fonte);

public record DashboardSyncResponse(
    LeadsStatsResponse Stats,
    DashboardSnapshotDto Snapshot);

public record CreateSnapshotRequest(
    DateTime? From,
    DateTime? To,
    DateTime? PrevFrom,
    DateTime? PrevTo,
    string? Fonte);

public record DashboardSnapshotDto(
    Guid Id,
    Guid EmpresaId,
    Guid CapturedByUserId,
    string CapturedByName,
    DateTime CapturedAt,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string Source,
    LeadsStatsResponse Stats);

public record DashboardSnapshotSummaryDto(
    Guid Id,
    Guid EmpresaId,
    Guid CapturedByUserId,
    string CapturedByName,
    DateTime CapturedAt,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string Source);

// ========== MotivoNaoFechamento ==========

public record CreateMotivoNaoFechamentoRequest(
    [Required, MaxLength(150)] string Nome,
    [Required, MaxLength(20)] string Cor);

public record MotivoNaoFechamentoDto(
    Guid Id,
    Guid EmpresaId,
    string Nome,
    string Cor,
    bool IsDefault,
    DateTime CreatedAt);

// ========== Audit Log ==========

public record AuditLogEntryDto(
    Guid Id,
    Guid EmpresaId,
    Guid? UserId,
    string? UserName,
    string? UserEmail,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? EntityLabel,
    IReadOnlyList<string>? ChangedFields,
    string? Ip,
    DateTime At);

public record AuditLogPageDto(
    List<AuditLogEntryDto> Items,
    int Total,
    int Page,
    int PageSize);

// ========== Kommo ==========

public record KommoConfigDto(
    string Subdomain,
    bool HasToken,
    string? TokenSuffix,
    bool HasWebhookSecret,
    DateTime? LastSyncAt);

public record SaveKommoConfigRequest(
    string Subdomain,
    string AccessToken,
    string? WebhookSecret);

public record KommoSyncRequest(
    int? Limit,
    int? Page,
    string? Query,
    DateTime? CreatedAtFrom,
    DateTime? CreatedAtTo);

public record KommoSyncResponse(int Received, int Stored, DateTime LastSyncAt);

public record KommoInboxItemDto(
    Guid Id,
    Guid EmpresaId,
    long? KommoLeadId,
    string Source,
    DateTime ReceivedAt,
    string Status,
    Guid? ImportedLeadId,
    string? Note,
    string Raw);

public record PromoteKommoLeadRequest(CreateLeadRequest Lead);
