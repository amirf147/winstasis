using System;
using System.IO;

namespace WinStasis.Storage
{
    public static class ProfileManager
    {
        private static readonly string SessionsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions");

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
    }
}
