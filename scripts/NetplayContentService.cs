using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JimmiLauncher;

public sealed class NetplayContentService
{
    private static readonly string _userAppdataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private readonly Uri _contentBaseUrl;
    private readonly string _coreBuildId;
    private readonly string _publicKeyPem;

    public NetplayContentService(Uri contentBaseUrl, string coreBuildId, string publicKeyPem)
    {
        _contentBaseUrl = contentBaseUrl;
        _coreBuildId = coreBuildId;
        _publicKeyPem = publicKeyPem;
    }

    public async Task<Content?> GetContentAttestationAsync(string romPath)
    {
        var manifestJsonUrl = new Uri(_contentBaseUrl, $"content/manifest.json");
        var manifestSigUrl = new Uri(_contentBaseUrl, $"content/manifest.sig");

        using var httpClient = new HttpClient();
        var manifestJsonBytes = await httpClient.GetByteArrayAsync(manifestJsonUrl);
        var manifestSigBase64 = await httpClient.GetStringAsync(manifestSigUrl);

        ManifestVerifier.VerifyOrThrow(manifestJsonBytes, manifestSigBase64, _publicKeyPem);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<ContentManifest>(manifestJsonBytes);
        if (manifest == null)
            throw new InvalidOperationException("Failed to deserialize content manifest.");
        if (manifest.Bundles.Length == 0)
            throw new InvalidOperationException("Content manifest contains no bundles.");
        
        var romMd5 = Globals.GetRomMd5(romPath).ToUpper();

        var manifestBundle = manifest?.Bundles?.FirstOrDefault(b =>
            b?.Compat?.CoreBuildId == _coreBuildId && b?.Compat?.RomMd5 == romMd5
        );
        if (manifestBundle == null)
            throw new InvalidOperationException("No compatible content bundle found in manifest.");
        
        var metadataUrl = new Uri(_contentBaseUrl, $"{manifestBundle.Metadata.Key}");
        var savestateUrl = new Uri(_contentBaseUrl, $"{manifestBundle.Savestate.Key}");
        var metadataLocalPath = Path.Combine(_userAppdataFolder, "Jimmi", "NetplayContent", manifestBundle.Metadata.Key);
        var savestateLocalPath = Path.Combine(_userAppdataFolder, "Jimmi", "NetplayContent", manifestBundle.Savestate.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(metadataLocalPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(savestateLocalPath)!);

        if (!File.Exists(metadataLocalPath) || !VerifyFileSha256(metadataLocalPath, manifestBundle.Metadata.Sha256))
        {
            Console.WriteLine($"Downloading content metadata from {metadataUrl} to {metadataLocalPath}");
            using var response = await httpClient.GetAsync(metadataUrl);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(metadataLocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        if (!VerifyFileSha256(metadataLocalPath, manifestBundle.Metadata.Sha256))
            throw new InvalidOperationException($"Downloaded file {manifestBundle.Metadata.Key} failed SHA256 verification.");
        
        if (!File.Exists(savestateLocalPath) || !VerifyFileSha256(savestateLocalPath, manifestBundle.Savestate.Sha256))
        {
            Console.WriteLine($"Downloading content savestate from {savestateUrl} to {savestateLocalPath}");
            using var response = await httpClient.GetAsync(savestateUrl);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(savestateLocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        if (!VerifyFileSha256(savestateLocalPath, manifestBundle.Savestate.Sha256))
            throw new InvalidOperationException($"Downloaded file {manifestBundle.Savestate.Key} failed SHA256 verification.");

        return new Content {
            Id = manifestBundle.Id,
            Compat = new BundleCompat {
                CoreBuildId = manifestBundle.Compat.CoreBuildId,
                RomMd5 = manifestBundle.Compat.RomMd5
            },
            Metadata = new BundleData {
                Key = manifestBundle.Metadata.Key,
                Sha256 = manifestBundle.Metadata.Sha256
            },
            Savestate = new BundleData {
                Key = manifestBundle.Savestate.Key,
                Sha256 = manifestBundle.Savestate.Sha256
            }
        };
    }

    private bool VerifyFileSha256(string localPath, string sha256)
    {
        using var sha256Alg = SHA256.Create();
        using var stream = File.OpenRead(localPath);
        var hashBytes = sha256Alg.ComputeHash(stream);
        var computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return computedHash == sha256;
    }

    public async Task<Content> EnsureRequiredContentAsync(Content req, string romPath)
    {
        // 1) Validate local launcher build matches req.coreBuildId
        if (!string.Equals(_coreBuildId, req.Compat.CoreBuildId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Core build mismatch.");

        // 2) Validate ROM md5 matches req.romMd5
        if (!string.Equals(Globals.GetRomMd5(romPath), req.Compat.RomMd5, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ROM mismatch.");


        // 3) Ensure the two files by key + sha256 (download if needed)
        var metadataLocalPath = Path.Combine(_userAppdataFolder, "Jimmi", "NetplayContent", req.Metadata.Key);
        var savestateLocalPath = Path.Combine(_userAppdataFolder, "Jimmi", "NetplayContent", req.Savestate.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(metadataLocalPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(savestateLocalPath)!);
        
        if (!VerifyFileSha256(metadataLocalPath, req.Metadata.Sha256))
        {
            using var httpClient = new HttpClient();
            var metadataUrl = new Uri(_contentBaseUrl, $"content/{req.Metadata.Key}");
            using var response = await httpClient.GetAsync(metadataUrl);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(metadataLocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        if (!VerifyFileSha256(savestateLocalPath, req.Savestate.Sha256))
        {
            using var httpClient = new HttpClient();
            var savestateUrl = new Uri(_contentBaseUrl, $"content/{req.Savestate.Key}");
            using var response = await httpClient.GetAsync(savestateUrl);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(savestateLocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        // 4) Return attestation for relay + paths for emulator
        return new Content {
            Id = req.Id,
            Compat = req.Compat,
            Metadata = new BundleData {
                Key = metadataLocalPath,
                Sha256 = req.Metadata.Sha256
            },
            Savestate = new BundleData {
                Key = savestateLocalPath,
                Sha256 = req.Savestate.Sha256
            }
        };
    }

    public async Task DownloadAllManifestContentAsync()
    {
        var manifestJsonUrl = new Uri(_contentBaseUrl, $"content/manifest.json");
        var manifestSigUrl = new Uri(_contentBaseUrl, $"content/manifest.sig");

        using var httpClient = new HttpClient();
        var manifestJsonBytes = await httpClient.GetByteArrayAsync(manifestJsonUrl);
        var manifestSigBase64 = await httpClient.GetStringAsync(manifestSigUrl);

        ManifestVerifier.VerifyOrThrow(manifestJsonBytes, manifestSigBase64, _publicKeyPem);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<ContentManifest>(manifestJsonBytes);
        if (manifest == null)
            throw new InvalidOperationException("Failed to deserialize content manifest.");
        if (manifest.Bundles.Length == 0)
            throw new InvalidOperationException("Content manifest contains no bundles.");

        foreach (var bundle in manifest.Bundles)
        {
            await DownloadContentFileAsync(httpClient, bundle.Metadata.Key, bundle.Metadata.Sha256);
            await DownloadContentFileAsync(httpClient, bundle.Savestate.Key, bundle.Savestate.Sha256);
        }
    }

    private async Task DownloadContentFileAsync(HttpClient httpClient, string fileKey, string expectedSha256)
    {
        var localPath = Path.Combine(_userAppdataFolder, "Jimmi", "NetplayContent", fileKey);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        if (File.Exists(localPath) && VerifyFileSha256(localPath, expectedSha256))
        {
            Console.WriteLine($"File already cached: {fileKey}");
            return;
        }

        Console.WriteLine($"Downloading content file from S3: {fileKey} to {localPath}");
        var fileUrl = new Uri(_contentBaseUrl, $"content/{fileKey}");
        using var response = await httpClient.GetAsync(fileUrl);
        response.EnsureSuccessStatusCode();
        using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);

        if (!VerifyFileSha256(localPath, expectedSha256))
            throw new InvalidOperationException($"Downloaded file {fileKey} failed SHA256 verification.");
    }
}

public class ContentManifest
{
    [JsonPropertyName("bundles")]
    public ContentManifestBundle[] Bundles { get; set; } = Array.Empty<ContentManifestBundle>();
    [JsonPropertyName("generatedUtc")]
    public string GeneratedUtc { get; set; } = string.Empty;
    [JsonPropertyName("manifestVersion")]
    public int ManifestVersion { get; set; }
}

public class ContentManifestBundle
{

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("compat")]
    public BundleCompat Compat { get; set; } = new BundleCompat();
    [JsonPropertyName("metadata")]
    public BundleData Metadata { get; set; } = new BundleData();
    [JsonPropertyName("savestate")]
    public BundleData Savestate { get; set; } = new BundleData();
}

public class BundleCompat
{
    [JsonPropertyName("coreBuildId")]
    public string CoreBuildId { get; set; } = string.Empty;
    [JsonPropertyName("romMd5")]
    public string RomMd5 { get; set; } = string.Empty;
}

public class BundleData
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

public class Content
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("compat")]
    public BundleCompat Compat { get; set; } = new BundleCompat();
    [JsonPropertyName("metadata")]
    public BundleData Metadata { get; set; } = new BundleData();
    [JsonPropertyName("savestate")]
    public BundleData Savestate { get; set; } = new BundleData();
}
