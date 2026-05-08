using System.Text.Json;

namespace CadastraAI.API.Kommo;

public interface IKommoClient
{
    Task<KommoAccountInfo> PingAsync(string subdomain, string accessToken, CancellationToken ct);
    Task<List<JsonElement>> ListLeadsWithContactsAsync(
        string subdomain,
        string accessToken,
        int limit,
        int page,
        string? query,
        CancellationToken ct);
}

public record KommoAccountInfo(string? Name, string? Subdomain);
