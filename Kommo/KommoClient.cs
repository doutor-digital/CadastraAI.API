using System.Net.Http.Headers;
using System.Text.Json;

namespace CadastraAI.API.Kommo;

public class KommoClient(HttpClient http) : IKommoClient
{
    private static string BuildBase(string subdomain)
    {
        var s = subdomain.Trim();
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) s = s[7..];
        if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s[8..];
        if (s.EndsWith(".kommo.com", StringComparison.OrdinalIgnoreCase)) s = s[..^10];
        return $"https://{s}.kommo.com";
    }

    private async Task<JsonElement> GetAsync(string subdomain, string accessToken, string pathAndQuery, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BuildBase(subdomain)}{pathAndQuery}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Kommo {(int)response.StatusCode} {response.ReasonPhrase} — {Trim(body, 240)}",
                inner: null,
                statusCode: response.StatusCode);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    public async Task<KommoAccountInfo> PingAsync(string subdomain, string accessToken, CancellationToken ct)
    {
        var data = await GetAsync(subdomain, accessToken, "/api/v4/account", ct);
        var name = data.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var sub = data.TryGetProperty("subdomain", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        return new KommoAccountInfo(name, sub);
    }

    public async Task<List<JsonElement>> ListLeadsWithContactsAsync(
        string subdomain,
        string accessToken,
        int limit,
        int page,
        string? query,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 250);
        page = Math.Max(page, 1);
        var qsParts = new List<string>
        {
            $"limit={limit}",
            $"page={page}",
            "with=contacts",
        };
        if (!string.IsNullOrWhiteSpace(query)) qsParts.Add($"query={Uri.EscapeDataString(query)}");
        var path = $"/api/v4/leads?{string.Join('&', qsParts)}";

        var root = await GetAsync(subdomain, accessToken, path, ct);
        var leads = new List<JsonElement>();
        if (!root.TryGetProperty("_embedded", out var embedded)) return leads;
        if (!embedded.TryGetProperty("leads", out var arr) || arr.ValueKind != JsonValueKind.Array) return leads;

        foreach (var lead in arr.EnumerateArray())
        {
            JsonElement? contact = null;
            if (lead.TryGetProperty("_embedded", out var leadEmbedded)
                && leadEmbedded.TryGetProperty("contacts", out var contacts)
                && contacts.ValueKind == JsonValueKind.Array)
            {
                var firstContact = contacts.EnumerateArray().FirstOrDefault();
                if (firstContact.ValueKind == JsonValueKind.Object && firstContact.TryGetProperty("id", out var idEl))
                {
                    var cid = idEl.GetInt64();
                    try
                    {
                        var contactRoot = await GetAsync(subdomain, accessToken, $"/api/v4/contacts/{cid}", ct);
                        contact = contactRoot;
                    }
                    catch
                    {
                        // contact fetch é best-effort
                    }
                }
            }

            // Embrulha {lead, contact} num único JSON pra match com o payload que o front analisa.
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("lead");
                lead.WriteTo(writer);
                if (contact.HasValue)
                {
                    writer.WritePropertyName("contact");
                    contact.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            ms.Position = 0;
            using var doc = await JsonDocument.ParseAsync(ms, cancellationToken: ct);
            leads.Add(doc.RootElement.Clone());
        }

        return leads;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
