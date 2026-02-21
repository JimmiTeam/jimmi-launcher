

using System;
using System.IO;

namespace JimmiLauncher
{
    public static class Globals
    {
        public static bool ReplaysEnabled = false;
        public static string RemixRomPath = "./roms/remix.z64";
        public static string VanillaRomPath = "./roms/smash.z64";
        public static string ReplaysFolderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/Jimmi/replays/";
        public static string MupenExecutablePath = "./mupen/mupen64plus-ui-console.exe";

        public static bool UsingRaphnet = false;
        
        // Online Globals
        public static string? OnlineHostToken = null;
        public static string? OnlineClientToken = null;
        public static string? OnlineRoomCode = null;
        public static string? NetplayMetadataPath = null;
        public static string? NetplaySavestatePath = null;
        public static string? MatchTicketId = null;

        public static void InitializeGlobals()
        {
            RemixRomPath = DatabaseHandler.GetPath("RemixRom") ?? RemixRomPath;
            VanillaRomPath = DatabaseHandler.GetPath("VanillaRom") ?? VanillaRomPath;
            ReplaysFolderPath = DatabaseHandler.GetPath("ReplaysFolder") ?? ReplaysFolderPath;
            MupenExecutablePath = DatabaseHandler.GetPath("MupenExecutable") ?? MupenExecutablePath;
        }

        public static string GetRomMd5(string romPath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(romPath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
