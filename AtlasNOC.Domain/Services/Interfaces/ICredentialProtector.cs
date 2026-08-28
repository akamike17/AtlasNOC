using System;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface ICredentialProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
    byte[] ProtectBytes(byte[] plaintext);
    byte[] UnprotectBytes(byte[] ciphertext);
}