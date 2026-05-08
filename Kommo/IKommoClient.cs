using System.Text.Json;

namespace CadastraAI.API.Kommo;

public interface IKommoClient
{
    Task<KommoAccountInfo> PingAsync(string subdomain, string accessToken, CancellationToken ct);
    Task<List<JsonElement>> ListLeadsWithContactsAsync(
        string subdomain,
        string accessToken,
        KommoListLeadsOptions options,
        CancellationToken ct);
}

/// <summary>
/// Options para listagem de leads na Kommo. Date range é opcional e mapeia para o filtro
/// filter[created_at][from]=&filter[created_at][to]= (em segundos unix).
/// </summary>
public record KommoListLeadsOptions(
    int Limit = 50,
    int Page = 1,
    string? Query = null,
    DateTime? CreatedAtFrom = null,
    DateTime? CreatedAtTo = null);

public record KommoAccountInfo(string? Name, string? Subdomain);
