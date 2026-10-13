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


        // =====================================================================
        // CLI ENTRY POINT
        // =====================================================================
        [STAThread] // <-- THIS IS CRITICAL FOR THE VIRTUAL DESKTOP COM LIBRARY
        static void Main(string[] args)
        {
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
            Console.WriteLine("  winst save <profile> [--force]      Save the current window layout to a profile.");
            Console.WriteLine("  winst list <profile>                List all windows saved in a profile with their Target IDs.");
            Console.WriteLine("  winst restore <profile>             Restore all windows in a profile.");
            Console.WriteLine("  winst restore <profile> --target X  Restore only the window with Target ID 'X'.");
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
            bool force = args.Length > 2 && (args[2] == "--force" || args[2] == "-f");

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

            var capturedWindows = new List<WindowRecord>();
            var desktopHelper = new VirtualDesktopHelper();
            var env = new WindowsEnvironmentAdapter();
            int currentTargetId = 1;

            foreach (long hwndLong in env.GetVisibleWindows())
            {
                IntPtr hWnd = (IntPtr)hwndLong;
                string windowTitle = env.GetWindowTitle(hwndLong).Trim();
                if (string.IsNullOrEmpty(windowTitle)) continue;

                string processName = env.GetWindowProcessName(hwndLong);
                if (string.IsNullOrEmpty(processName)) processName = "Unknown";

                if (processName == "TextInputHost" || processName == "ApplicationFrameHost") continue;

                WindowPlacement placement = env.GetWindowPlacement(hwndLong);
                int x = placement.NormalPosition.Left;
                int y = placement.NormalPosition.Top;
                int width = placement.NormalPosition.Width;
                int height = placement.NormalPosition.Height;

                if (width <= 0 || height <= 0) continue;

                Guid desktopId = desktopHelper.GetWindowDesktopId(hWnd);
                bool isPinned = desktopHelper.IsWindowPinned(hWnd);

                capturedWindows.Add(new WindowRecord
                {
                    TargetId = currentTargetId++,
                    Hwnd = hwndLong,
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    ShowCmd = placement.ShowCmd,
                    DesktopId = desktopId,
                    IsPinned = isPinned
                });
            }

            var profile = new SessionProfile
            {
                ProfileName = profileName,
                CreatedAt = DateTime.Now,
                Windows = capturedWindows.ToArray()
            };

            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProfileManager.GetFilePath(profileName), json);

            Console.WriteLine($"Successfully saved {capturedWindows.Count} windows to profile '{profileName}'.");
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

            var desktopHelper = new VirtualDesktopHelper();

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
                    int deskNum = desktopHelper.GetDesktopNumber(win.DesktopId);
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

            // Parse --target argument if provided
            int? targetId = null;
            if (args.Length >= 4 && (args[2] == "--target" || args[2] == "-t"))
            {
                if (int.TryParse(args[3], out int parsedTarget))
                {
                    targetId = parsedTarget;
                }
                else
                {
                    Console.WriteLine("Error: Invalid target ID. Please provide a valid number.");
                    return;
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
            restorer.Restore(profile, targetId);
        }
    }
}