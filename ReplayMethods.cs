using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia.Controls.Primitives;

namespace JimmiLauncher
{
    public static class ReplayMethods
    {
        private static string ReplaysFolder = Globals.ReplaysFolderPath;
        private static string ds = Path.DirectorySeparatorChar.ToString();

        public static void CompressArchives(object? sender, EventArgs e)
        {
            var directoriesRaw = Directory.EnumerateDirectories(ReplaysFolder).Select(d => new DirectoryInfo(d).Name).ToList();

            foreach (var directory in directoriesRaw)
            {
                
                var replayDir = directory.Replace(".", ":");

                DateTime dateTime;
                if (DateTime.TryParse(replayDir, out dateTime))
                {
                    replayDir = dateTime.ToString();
                }
                else
                {
                    replayDir = directory;
                    
                }

                var folderPath = $"{ReplaysFolder}{ds}{directory}";
                using (ZipArchive zip = ZipFile.Open($"{folderPath}.jrpl", ZipArchiveMode.Create))
                {
                    var thumbnailPath = $"{folderPath}{ds}thumbnail.jpg";
                    var inputPath = $"{folderPath}{ds}input.bin";
                    var statePath = $"{folderPath}{ds}state.st";
                    var gameType = File.Exists($"{folderPath}{ds}remix") ? "remix" : "vanilla";
                    var gameTypePath = $"{folderPath}{ds}{gameType}";
                    zip.CreateEntryFromFile(thumbnailPath, "thumbnail.jpg", CompressionLevel.Optimal);
                    zip.CreateEntryFromFile(inputPath, "input.bin", CompressionLevel.Optimal);
                    zip.CreateEntryFromFile(statePath, "state.st", CompressionLevel.Optimal);
                    zip.CreateEntryFromFile(gameTypePath, gameType, CompressionLevel.Optimal);
                    File.Delete(thumbnailPath);
                    File.Delete(inputPath);
                    File.Delete(statePath);
                    File.Delete(gameTypePath);
                    Directory.Delete(folderPath);
                }
            }
        }

        public static string ExtractReplayData(string name)
        {
            using (ZipArchive zip = ZipFile.Open($"{ReplaysFolder}{ds}{name}", ZipArchiveMode.Read))
            {
                var extractPath = $"{ReplaysFolder}{ds}{name.Replace(".jrpl", "")}";
                Directory.CreateDirectory(extractPath);
                zip.GetEntry("input.bin")!.ExtractToFile($"{extractPath}{ds}input.bin");
                zip.GetEntry("state.st")!.ExtractToFile($"{extractPath}{ds}state.st");
                var gameType = File.Exists($"{extractPath}{ds}remix") ? "remix" : "vanilla";
                zip.GetEntry(gameType)!.ExtractToFile($"{extractPath}{ds}{gameType}");
                return extractPath;
            }
        }


        public static void ExtractThumbnails()
        {
            var replayFiles = Directory.EnumerateFiles(ReplaysFolder).Where(f => f.EndsWith(".jrlp")).ToList();

            foreach (var replay in replayFiles)
            {
                using (ZipArchive zip = ZipFile.Open($"{replay}", ZipArchiveMode.Read))
                {
                    DirectoryInfo newFolder= Directory.CreateDirectory($"./temp_thumbnails/{replay.Replace(".jrpl", "")}");
                    var newFolderName = replay.Replace(".jrpl", "");
                    zip.GetEntry("thumbnail.jpg")!.ExtractToFile($"{newFolder.Name}/{replay}");
                }
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
    }
}

