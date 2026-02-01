using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace JimmiLauncher 
{
    public class PathEntry
    {
        public int Id { get; set; }
        public string PathType { get; set; } = string.Empty;
        public string PathValue { get; set; } = string.Empty;
    }

    public class GameRom
    {
        public int Id { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string GamePath { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
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

            var gamesCollection = _database.GetCollection<GameRom>("GameRoms");
            gamesCollection.EnsureIndex(x => x.GamePath);

            // Insert default paths if they do not exist
            var defaultPaths = new List<(string Type, string Value)>
            {
                ("ReplaysFolder", $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/Jimmi/Replays/"),
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

        public static void AddGame(string name, string path, string id)
        {
            if (_database == null) return;
            var collection = _database.GetCollection<GameRom>("GameRoms");
            
            var existing = collection.FindOne(x => x.GamePath == path);
            if (existing == null)
            {
                collection.Insert(new GameRom { GameName = name, GamePath = path, GameId = id });
            }
            else 
            {
                existing.GameName = name;
                existing.GameId = id;
                collection.Update(existing);
            }
        }

        public static List<GameRom> GetGames()
        {
            if (_database == null) return new List<GameRom>();
            return _database.GetCollection<GameRom>("GameRoms").FindAll().ToList();
        }

    }
}
