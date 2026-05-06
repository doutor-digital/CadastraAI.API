using System.ComponentModel.DataAnnotations;
using CadastraAI.API.Models;

namespace CadastraAI.API.Dtos;

public record CreateEmpresaRequest(
    [Required, MaxLength(200)] string Nome,
    [MaxLength(100)] string? Tipo);

public record EmpresaDto(
    Guid Id,
    string Nome,
    string? Tipo,
    string? LogoUrl,
    Guid OwnerUserId,
    string OwnerName,
    DateTime CreatedAt,
    int MemberCount,
    MembershipRole MyRole);

public record MemberDto(
    Guid UserId,
    string Email,
    string Name,
    string? AvatarUrl,
    MembershipRole Role,
    DateTime JoinedAt);

public record CreateInviteRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    MembershipRole Role = MembershipRole.Member);

public record InviteDto(
    Guid Id,
    Guid EmpresaId,
    string EmpresaNome,
    string Email,
    MembershipRole Role,
    string Token,
    string InviteUrl,
    InviteStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    bool? EmailDelivered = null,
    string? EmailError = null);

public record InvitePreviewDto(
    string EmpresaNome,
    string Email,
    MembershipRole Role,
    InviteStatus Status,
    DateTime ExpiresAt,
    bool ExistingUser);

public record AcceptInviteWithPasswordRequest(
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(200)] string Name);

public record AcceptInviteWithGoogleRequest(
    [Required] string IdToken);
