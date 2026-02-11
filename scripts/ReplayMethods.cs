using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia.Controls.Primitives;
// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.Formats.Jpeg;
// using SixLabors.ImageSharp.Processing;

namespace JimmiLauncher
{
    public static class ReplayMethods
    {
        private static string ReplaysFolder = Globals.ReplaysFolderPath;

        public static void CompressArchives(object? sender, EventArgs e)
        {
            var directoriesRaw = Directory.EnumerateDirectories(ReplaysFolder).Select(d => new DirectoryInfo(d).Name).ToList();

            foreach (var directory in directoriesRaw)
            {
                
                // var replayDir = directory.Replace(".", ":");

                // DateTime dateTime;
                // if (DateTime.TryParse(replayDir, out dateTime))
                // {
                //     replayDir = dateTime.ToString();
                // }
                // else
                // {
                //     replayDir = directory;
                // }

                var folderPath = $"{ReplaysFolder}/{directory}";
                using (ZipArchive zip = ZipFile.Open($"{folderPath}.jrpl", ZipArchiveMode.Create))
                {
                    // try
                    // {
                    //     var image = Image.Load($"{folderPath}/smash_remix-000.png");
                    //     var encoder = new JpegEncoder
                    //     {
                    //         Quality = 30
                    //     };
                    //     image.Mutate(x => x.Resize(320, 240));
                    //     image.Save($"{folderPath}/thumbnail.jpg", encoder);
                    //     File.Delete($"{folderPath}/smash_remix-000.png");

                    //     var thumbnailPath = $"{folderPath}/thumbnail.jpg";
                    //     var inputPath = $"{folderPath}/inputs.bin";
                    //     var statePath = $"{folderPath}/state.st";
                    //     var gameType = File.Exists($"{folderPath}/remix") ? "remix" : "vanilla";
                    //     var gameTypePath = $"{folderPath}/{gameType}";
                    //     zip.CreateEntryFromFile(thumbnailPath, "thumbnail.jpg", CompressionLevel.Fastest);
                    //     zip.CreateEntryFromFile(inputPath, "inputs.bin", CompressionLevel.SmallestSize);
                    //     zip.CreateEntryFromFile(statePath, "state.st", CompressionLevel.SmallestSize);
                    //     zip.CreateEntryFromFile(gameTypePath, gameType, CompressionLevel.Optimal);
                    //     File.Delete(thumbnailPath);
                    //     File.Delete(inputPath);
                    //     File.Delete(statePath);
                    //     File.Delete(gameTypePath);
                    //     Directory.Delete(folderPath);
                    // }
                    // catch (Exception ex)
                    // {
                    //     Console.WriteLine($"Error compressing replay archive: {ex.Message}");
                    // }
                }
            }
        }

        public static string ExtractReplayData(string name)
        {
            using (ZipArchive zip = ZipFile.Open($"{ReplaysFolder}/{name}", ZipArchiveMode.Read))
            {
                var extractPath = $"{ReplaysFolder}/{name.Replace(".jrpl", "")}";
                Directory.CreateDirectory(extractPath);
                zip.GetEntry("inputs.bin")!.ExtractToFile($"{extractPath}/inputs.bin", true);
                zip.GetEntry("state.st")!.ExtractToFile($"{extractPath}/state.st", true);
                
                var gameType = zip.GetEntry("remix") != null ? "remix" : "vanilla";
                if (zip.GetEntry(gameType) != null)
                {
                    zip.GetEntry(gameType)!.ExtractToFile($"{extractPath}/{gameType}", true);
                }
                
                return extractPath;
            }
        }


        public static void ExtractThumbnails()
        {
            try
            {
                if (!Directory.Exists(ReplaysFolder))
                {
                    Console.WriteLine($"Replays folder not found: {ReplaysFolder}");
                    return;
                }

                var replayFiles = Directory.EnumerateFiles(ReplaysFolder).Where(f => f.EndsWith(".jrpl")).ToList();
                Directory.CreateDirectory("./temp_thumbnails");
                foreach (var replay in replayFiles)
                {
                    using (ZipArchive zip = ZipFile.Open($"{replay}", ZipArchiveMode.Read))
                    {
                        zip.GetEntry("thumbnail.jpg")!.ExtractToFile($"./temp_thumbnails/{new FileInfo(replay).Name.Replace(".jrpl", ".jpg")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting thumbnails: {ex.Message}");
            }
        }

        public static void DeleteThumbnails()
        {
            foreach (var file in Directory.GetFiles("./temp_thumbnails"))
            {
                File.Delete(file);
            }
            Directory.Delete("./temp_thumbnails");
        }

        public static void DeleteExtractedReplayData(object? sender, EventArgs e)
        {
            foreach (var dir in Directory.GetDirectories(ReplaysFolder))
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    File.Delete(file);
                }
                Directory.Delete(dir);
            }
        }
    }
}

