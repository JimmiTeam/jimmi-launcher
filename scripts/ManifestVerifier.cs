using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static class ManifestVerifier
{
    private static bool VerifyManifest(byte[] manifestJsonBytes, string manifestSigBase64, string publicKeyPem)
    {
        byte[] sig;
        try
        {
            sig = Convert.FromBase64String(manifestSigBase64.Trim());
        }
        catch
        {
            return false;
        }

        var ecdsa = ECDsa.Create();
        var pubDer = ReadPemBlock(publicKeyPem, "PUBLIC KEY");
        ecdsa.ImportSubjectPublicKeyInfo(pubDer, out _);

        return ecdsa.VerifyData(manifestJsonBytes, sig, HashAlgorithmName.SHA256);
    }

    private static byte[] ReadPemBlock(string pem, string label)
    {
        var header = $"-----BEGIN {label}-----";
        var footer = $"-----END {label}-----";
        var start = pem.IndexOf(header, StringComparison.Ordinal);
        var end = pem.IndexOf(footer, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new Exception($"PEM block {label} not found.");
        }
        start += header.Length;
        var b64 = pem[start..end].Replace("\r", "").Replace("\n", "").Trim();
        return Convert.FromBase64String(b64);
    }

    public static void VerifyOrThrow(byte[] manifestBytes, string sigB64, string pubPem)
    {
        if (!VerifyManifest(manifestBytes, sigB64, pubPem))
            throw new InvalidOperationException("Manifest signature invalid.");
    }
}
