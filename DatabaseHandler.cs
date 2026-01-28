using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;

namespace JimmiLauncher 
{
    public class PathEntry
    {
        public int Id { get; set; }
        public string PathType { get; set; } = string.Empty;
        public string PathValue { get; set; } = string.Empty;
    }

    public static class DatabaseHandler
    {
        private static LiteDatabase? _database;

        public static void InitializeDatabase()
        {
            var current_dir = Directory.GetCurrentDirectory();
            var dbPath = Path.Combine(current_dir, "jimmi_database.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            Console.WriteLine($"Database path: {dbPath}");

            _database = new LiteDatabase($"Filename={dbPath}; Connection=shared;");

            // Ensure the collection exists and has an index
            var collection = _database.GetCollection<PathEntry>("Paths");
            collection.EnsureIndex(x => x.PathType);

            // Insert default paths if they do not exist
            var defaultPaths = new List<(string Type, string Value)>
            {
                ("RemixRom", "E:\\Jimmi\\JimmiLauncher\\roms\\remix.z64"),
                ("VanillaRom", "E:\\Jimmi\\JimmiLauncher\\roms\\smash.z64"),
                ("ReplaysFolder", "E:\\Jimmi\\JimmiLauncher\\replays\\"),
                ("MupenExecutable", "E:\\Jimmi\\mupen64plus-ui-console\\projects\\msvc\\x64\\Release\\mupen64plus-ui-console.exe")
            };

            foreach (var (pathType, pathValue) in defaultPaths)
            {
                if (collection.FindOne(x => x.PathType == pathType) == null)
                {
                    collection.Insert(new PathEntry { PathType = pathType, PathValue = pathValue });
                }
            }
        }

        public static void CloseDatabase()
        {
            _database?.Dispose();
        }

        public static string? GetPath(string pathType)
        {
            if (_database == null) return null;

            var collection = _database.GetCollection<PathEntry>("Paths");
            var entry = collection.FindOne(x => x.PathType == pathType);
            return entry?.PathValue;
        }
    }
}
