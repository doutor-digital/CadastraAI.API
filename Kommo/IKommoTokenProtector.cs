using Microsoft.AspNetCore.DataProtection;

namespace CadastraAI.API.Kommo;

/// <summary>
/// Pequena fachada sobre IDataProtector com purpose fixo, pra facilitar mocking em testes
/// e garantir que todo lugar que cifra/decifra token Kommo use a mesma chave de derivação.
/// </summary>
public interface IKommoTokenProtector
{
    string Encrypt(string plain);
    string Decrypt(string cipher);
}

public class KommoTokenProtector : IKommoTokenProtector
{
    private readonly IDataProtector _protector;

    public KommoTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("CadastraAI.Kommo.AccessToken.v1");
    }

    public string Encrypt(string plain) => _protector.Protect(plain);
    public string Decrypt(string cipher) => _protector.Unprotect(cipher);
}
