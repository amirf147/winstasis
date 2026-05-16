using System;
using System.IO;

namespace WinStasis.Storage
{
    public static class ProfileManager
    {
        private static readonly string SessionsDir = FindSessionsDirectory();

        private static string FindSessionsDirectory()
        {
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(currentDir))
            {
                var sessionsPath = Path.Combine(currentDir, "sessions");
                // Avoid picking up 'sessions' inside bin/obj folders
                bool isInsideBuildFolder = currentDir.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || 
                                           currentDir.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

                if (Directory.Exists(sessionsPath) && !isInsideBuildFolder)
                {
                    return sessionsPath;
                }
                currentDir = Path.GetDirectoryName(currentDir);
            }
            // Fallback to local sessions if not found
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions");
        }

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
