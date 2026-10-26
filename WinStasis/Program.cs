using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Linq;
using WinStasis.Models;
using WinStasis.Storage;
using WinStasis.Adapters;

namespace WinStasis
{
    class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        // =====================================================================
        // CLI ENTRY POINT
        // =====================================================================
        [STAThread] // <-- THIS IS CRITICAL FOR THE VIRTUAL DESKTOP COM LIBRARY
        static void Main(string[] args)
        {
            SetProcessDpiAwarenessContext((IntPtr)(-4));
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "save":
                    HandleSaveCommand(args);
                    break;
                case "list":
                    HandleListCommand(args);
                    break;
                case "restore":
                    HandleRestoreCommand(args);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    ShowHelp();
                    break;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("🪟 winst - Window State Manager\n");
            Console.WriteLine("Usage:");
            Console.WriteLine("  winst save <profile> [--force|-f] [--verbose|-v]      Save the current window layout to a profile.");
            Console.WriteLine("  winst list <profile>                                  List all windows saved in a profile with their Target IDs.");
            Console.WriteLine("  winst restore <profile> [--verbose|-v]                Restore all windows in a profile.");
            Console.WriteLine("  winst restore <profile> [--target|-t X] [--verbose|-v] Restore only the window with Target ID 'X'.");
        }

        // =====================================================================
        // COMMAND HANDLERS
        // =====================================================================
        private static void HandleSaveCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winst save coding')");
                return;
            }

            string profileName = args[1];
            bool force = args.Skip(2).Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase) || a.Equals("-f", StringComparison.OrdinalIgnoreCase));
            bool verbose = args.Skip(2).Any(a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase) || a.Equals("-v", StringComparison.OrdinalIgnoreCase));

            ProfileManager.EnsureDirectoryExists();

            if (!force && ProfileManager.ProfileExists(profileName))
            {
                Console.Write($"Profile '{profileName}' already exists. Overwrite? (y/n): ");
                var key = Console.ReadKey().KeyChar;
                Console.WriteLine();
                if (key != 'y' && key != 'Y')
                {
                    Console.WriteLine("Save aborted.");
                    return;
                }
            }

            Console.WriteLine($"Scanning visible windows for profile: {profileName}...");

            var env = new WindowsEnvironmentAdapter();
            var capturer = new WindowCapturer(env);
            SessionProfile profile = capturer.Capture(profileName, verbose);

            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProfileManager.GetFilePath(profileName), json);

            Console.WriteLine($"Successfully saved {profile.Windows.Length} windows to profile '{profileName}'.");
            Console.WriteLine($"Storage Location: {ProfileManager.GetFilePath(profileName)}");
        }

        private static void HandleListCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winst list coding')");
                return;
            }

            string profileName = args[1];
            if (!ProfileManager.ProfileExists(profileName))
            {
                Console.WriteLine($"Error: Profile '{profileName}' not found.");
                return;
            }

            string json = File.ReadAllText(ProfileManager.GetFilePath(profileName));
            var profile = JsonSerializer.Deserialize<SessionProfile>(json);

            if (profile == null || profile.Windows.Length == 0)
            {
                Console.WriteLine($"Profile '{profileName}' is empty.");
                return;
            }

            var env = new WindowsEnvironmentAdapter();

            Console.WriteLine($"Profile: {profile.ProfileName} (Saved: {profile.CreatedAt})");
            Console.WriteLine(new string('-', 80));

            foreach (var win in profile.Windows)
            {
                string workspaceStr;

                if (win.IsPinned)
                {
                    workspaceStr = "[WS: Pinned]";
                }
                else if (win.DesktopId != Guid.Empty)
                {
                    int deskNum = env.GetDesktopNumber(win.DesktopId);
                    workspaceStr = deskNum > 0 ? $"[WS: Desk {deskNum}]" : $"[WS: {win.DesktopId.ToString().Substring(0, 8)}...]";
                }
                else
                {
                    workspaceStr = "[WS: Global]";
                }

                Console.WriteLine($"[{win.TargetId:D2}] {workspaceStr,-14} {win.ProcessName}.exe - \"{win.WindowTitle}\"");
            }
            Console.WriteLine(new string('-', 80));
        }

        private static void HandleRestoreCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winst restore coding')");
                return;
            }

            string profileName = args[1];
            if (!ProfileManager.ProfileExists(profileName))
            {
                Console.WriteLine($"Error: Profile '{profileName}' not found.");
                return;
            }

            bool verbose = false;
            int? targetId = null;

            var remainingArgs = args.Skip(2).ToArray();
            for (int i = 0; i < remainingArgs.Length; i++)
            {
                string arg = remainingArgs[i];
                if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) || arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
                {
                    verbose = true;
                }
                else if (arg.Equals("--target", StringComparison.OrdinalIgnoreCase) || arg.Equals("-t", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < remainingArgs.Length && int.TryParse(remainingArgs[i + 1], out int parsedTarget))
                    {
                        targetId = parsedTarget;
                        i++;
                    }
                    else
                    {
                        Console.WriteLine("Error: Invalid target ID. Please provide a valid number.");
                        return;
                    }
                }
            }

            string json = File.ReadAllText(ProfileManager.GetFilePath(profileName));
            var profile = JsonSerializer.Deserialize<SessionProfile>(json);

            if (profile == null || profile.Windows.Length == 0)
            {
                Console.WriteLine($"Profile '{profileName}' is empty or corrupted.");
                return;
            }

            var env = new WindowsEnvironmentAdapter();
            var restorer = new WindowRestorer(env);
            restorer.Restore(profile, targetId, verbose);
        }
    }
}