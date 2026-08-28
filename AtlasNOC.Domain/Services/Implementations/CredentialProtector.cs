using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public sealed class CredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector("AtlasNOC.Credentials.v1");
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);
        return _protector.Unprotect(ciphertext);
    }

    public byte[] ProtectBytes(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return _protector.Protect(plaintext);
    }

    public byte[] UnprotectBytes(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        return _protector.Unprotect(ciphertext);
    }
}