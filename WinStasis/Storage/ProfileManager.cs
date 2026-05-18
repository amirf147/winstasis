using System;
using System.IO;

namespace WinStasis.Storage
{
    public static class ProfileManager
    {
        // Migrate storage to the standard Windows AppData/Local folder
        private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinStasis");
        private static readonly string SessionsDir = Path.Combine(AppDataDir, "sessions");

        public static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(SessionsDir))
            {
                Directory.CreateDirectory(SessionsDir);
            }
        }

        public static string GetFilePath(string profileName)
        {
            // Simple sanitization to prevent directory traversal
            var safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(SessionsDir, $"{safeName}.json");
        }

        public static bool ProfileExists(string profileName)
        {
            return File.Exists(GetFilePath(profileName));
        }

        public static string GetStorageLocation()
        {
            return SessionsDir;
        }
    }
}
