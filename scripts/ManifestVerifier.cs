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
            // Strip all whitespace from the base64 string (handles multi-line signatures)
            var cleanedBase64 = new string(manifestSigBase64.Where(c => !char.IsWhiteSpace(c)).ToArray());
            Console.WriteLine($"Signature base64 length (raw): {manifestSigBase64.Length}, (cleaned): {cleanedBase64.Length}");
            sig = Convert.FromBase64String(cleanedBase64);
            Console.WriteLine($"Decoded signature: {sig.Length} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to decode signature base64: {ex.Message}");
            return false;
        }

        var ecdsa = ECDsa.Create();
        var pubDer = ReadPemBlock(publicKeyPem, "PUBLIC KEY");
        ecdsa.ImportSubjectPublicKeyInfo(pubDer, out _);

        // Print SHA256 of manifest bytes so user can compare against signing side
        using var sha = SHA256.Create();
        var manifestHash = sha.ComputeHash(manifestJsonBytes);
        Console.WriteLine($"Manifest SHA256: {BitConverter.ToString(manifestHash).Replace("-", "").ToLowerInvariant()}");
        Console.WriteLine($"Manifest bytes: {manifestJsonBytes.Length}, Sig bytes: {sig.Length}");
        // Print first few bytes to check for BOM (EF BB BF)
        Console.WriteLine($"Manifest first 10 bytes: {BitConverter.ToString(manifestJsonBytes, 0, Math.Min(10, manifestJsonBytes.Length))}");
        // Print loaded public key for verification
        var exportedKey = ecdsa.ExportSubjectPublicKeyInfo();
        Console.WriteLine($"Loaded public key (base64): {Convert.ToBase64String(exportedKey)}");

        var derResult = ecdsa.VerifyData(manifestJsonBytes, sig, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        Console.WriteLine($"DER verify: {derResult}");
        if (derResult) return true;

        var ieeeResult = ecdsa.VerifyData(manifestJsonBytes, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Console.WriteLine($"IEEE P1363 verify: {ieeeResult}");
        return ieeeResult;
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
        Console.WriteLine($"Verifying manifest with signature: {sigB64}");
        if (!VerifyManifest(manifestBytes, sigB64, pubPem))
        {
            Console.WriteLine("Manifest signature invalid!");
            throw new InvalidOperationException("Manifest signature invalid.");
        }
        else
            Console.WriteLine("Manifest signature valid.");
    }
}
