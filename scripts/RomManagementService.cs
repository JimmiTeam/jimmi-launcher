using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace JimmiLauncher
{
    public class RomManagementService
    {
        private static readonly Uri _contentBaseUrl = new Uri("https://jimmi-netplay-content.s3.us-east-2.amazonaws.com/");
        private static readonly string _publicKeyPem = @"-----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE1ctV5EzPJyse4WQ/9xX3pMkgO26P
        GK+qsILgR05vJVta7l2KoB93AStYqC54kyYYvsZYYbs0flgHkGdUu8an2g==
        -----END PUBLIC KEY-----";

        private static readonly string _userApppdataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public async Task AddRomAsync(string romPath)
        {
            if (!File.Exists(romPath))
                throw new FileNotFoundException("ROM file not found", romPath);

            var manifest = await GetManifestAsync();

            var romMd5 = Globals.GetRomMd5(romPath).ToUpper();

            var bundle = manifest.Bundles.FirstOrDefault(b => b.Compat.RomMd5 == romMd5);

            if (bundle == null)
            {
                throw new Exception("This ROM is not recognized by the manifest. Please ensure you have a supported ROM version.");
            }

            var metadataPath = await EnsureMetadataAsync(bundle);

            var metadataContent = await File.ReadAllTextAsync(metadataPath);
            using var doc = JsonDocument.Parse(metadataContent);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("rom", out var romProp))
            {
                if (romProp.TryGetProperty("sha256", out var sha256Prop))
                {
                    var expectedSha256 = sha256Prop.GetString();
                    var actualSha256 = GetSha256(romPath);
                    if (!string.Equals(expectedSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception("ROM SHA256 verification failed against metadata.");
                    }
                }
                
                if (romProp.TryGetProperty("expectedSizeBytes", out var sizeProp))
                {
                    var expectedSize = sizeProp.GetInt64();
                    var actualSize = new FileInfo(romPath).Length;
                    if (expectedSize != actualSize)
                    {
                        throw new Exception($"ROM size mismatch. Expected {expectedSize}, got {actualSize}.");
                    }
                }
            }

            string gameName = root.TryGetProperty("gameName", out var nameProp) ? nameProp.GetString() ?? "Unknown Game" : "Unknown Game";

            // 6. Add to Database
            DatabaseHandler.AddGame(gameName, romPath, bundle.Id);
        }

        private async Task<ContentManifest> GetManifestAsync()
        {
             try 
             {
                using var httpClient = new HttpClient();
                var manifestJsonUrl = new Uri(_contentBaseUrl, "content/manifest.json");
                var manifestSigUrl = new Uri(_contentBaseUrl, "content/manifest.sig");

                var manifestJsonBytes = await httpClient.GetByteArrayAsync(manifestJsonUrl);
                var manifestSigBase64 = await httpClient.GetStringAsync(manifestSigUrl);

                ManifestVerifier.VerifyOrThrow(manifestJsonBytes, manifestSigBase64, _publicKeyPem);
                var manifest = JsonSerializer.Deserialize<ContentManifest>(manifestJsonBytes);
                return manifest ?? throw new Exception("Failed to deserialize manifest.");
             }
             catch (Exception ex)
             {
                 Console.WriteLine("Failed to fetch manifest from S3: " + ex.Message);
                 throw;
             }
        }

        private async Task<string> EnsureMetadataAsync(ContentManifestBundle bundle)
        {
             var relativePath = bundle.Metadata.Key;
             var localPath = Path.Combine(_userApppdataFolder, "Jimmi", "NetplayContent", relativePath);
             Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

             if (File.Exists(localPath)) 
             {
                 if (VerifyFileSha256(localPath, bundle.Metadata.Sha256))
                     return localPath;
             }

             using var httpClient = new HttpClient();
             // bundle.Metadata.Key is full relative path e.g. "content/metadata/..."
             var response = await httpClient.GetAsync(new Uri(_contentBaseUrl, bundle.Metadata.Key));
             response.EnsureSuccessStatusCode();
             using (var fs = new FileStream(localPath, FileMode.Create))
             {
                 await response.Content.CopyToAsync(fs);
             }

             if (!VerifyFileSha256(localPath, bundle.Metadata.Sha256))
                 throw new Exception("Downloaded metadata SHA256 mismatch.");

             return localPath;
        }

        private bool VerifyFileSha256(string path, string expectedSha256)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        private string GetSha256(string path)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha256.ComputeHash(stream);
             return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
