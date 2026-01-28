

namespace JimmiLauncher
{
    public static class Globals
    {
        public static bool ReplaysEnabled = false;
        public static string RemixRomPath = "./roms/remix.z64";
        public static string VanillaRomPath = "./roms/smash.z64";
        public static string ReplaysFolderPath = "./replays/";
        public static string MupenExecutablePath = "../mupen64plus-ui-console/projects/msvc/x64/Release/mupen64plus-ui-console.exe";

        public static void InitializeGlobals()
        {
            RemixRomPath = DatabaseHandler.GetPath("RemixRom") ?? RemixRomPath;
            VanillaRomPath = DatabaseHandler.GetPath("VanillaRom") ?? VanillaRomPath;
            ReplaysFolderPath = DatabaseHandler.GetPath("ReplaysFolder") ?? ReplaysFolderPath;
            MupenExecutablePath = DatabaseHandler.GetPath("MupenExecutable") ?? MupenExecutablePath;
        }
    }
}
