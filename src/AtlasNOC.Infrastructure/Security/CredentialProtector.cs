using Microsoft.AspNetCore.DataProtection;

namespace AtlasNOC.Infrastructure.Security;

/// <summary>Cifra/descifra secretos de credenciales usando Data Protection.</summary>
public interface ICredentialProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

public class CredentialProtector : ICredentialProtector
{
    private static readonly string Purpose = "AtlasNOC.DeviceCredential.v1";
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return string.Empty;
        return _protector.Unprotect(protectedValue);
    }
}