using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.IO;
using WinStasis.Models;
using WinStasis.Storage;

namespace WinStasis
{
    class Program
    {
        // =====================================================================
        // WIN32 API IMPORTS (P/Invoke) - Restored from Phase 1
        // =====================================================================
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        // =====================================================================
        // CLI ENTRY POINT
        // =====================================================================
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
            Console.WriteLine("🪟 winstasis - Window State Manager\n");
            Console.WriteLine("Usage:");
            Console.WriteLine("  winstasis save <profile> [--force]      Save the current window layout to a profile.");
            Console.WriteLine("  winstasis list <profile>                List all windows saved in a profile with their Target IDs.");
            Console.WriteLine("  winstasis restore <profile>             Restore all windows in a profile.");
            Console.WriteLine("  winstasis restore <profile> --target X  Restore only the window with Target ID 'X'.");
        }

        // =====================================================================
        // COMMAND HANDLERS
        // =====================================================================
        private static void HandleSaveCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winstasis save coding')");
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
            int currentTargetId = 1;

            // Define the callback inline for easy access to captured variables
            bool CaptureWindowCallback(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;

                StringBuilder titleBuilder = new(256);
                _ = GetWindowText(hWnd, titleBuilder, 256);
                string windowTitle = titleBuilder.ToString().Trim();

                if (string.IsNullOrEmpty(windowTitle)) return true;

                _ = GetWindowThreadProcessId(hWnd, out uint processId);
                string processName = "Unknown";
                try
                {
                    using Process proc = Process.GetProcessById((int)processId);
                    processName = proc.ProcessName;
                }
                catch (Exception) { /* Ignore access denied processes */ }

                if (processName == "TextInputHost" || processName == "ApplicationFrameHost") return true;

                GetWindowRect(hWnd, out RECT rect);
                
                WINDOWPLACEMENT placement = new() { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                GetWindowPlacement(hWnd, ref placement);

                // Extract Omniscient Workspace GUID
                Guid desktopId = desktopHelper.GetWindowDesktopId(hWnd);

                capturedWindows.Add(new WindowRecord
                {
                    TargetId = currentTargetId++,
                    Hwnd = hWnd.ToInt64(), // Convert to long for JSON
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Right - rect.Left,
                    Height = rect.Bottom - rect.Top,
                    ShowCmd = placement.showCmd,
                    DesktopId = desktopId
                });

                return true;
            }

            // Run the P/Invoke Scan
            EnumWindows(CaptureWindowCallback, IntPtr.Zero);

            // Serialize and Save
            var profile = new SessionProfile
            {
                ProfileName = profileName,
                CreatedAt = DateTime.Now,
                Windows = capturedWindows.ToArray()
            };

            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProfileManager.GetFilePath(profileName), json);

            Console.WriteLine($"Successfully saved {capturedWindows.Count} windows to profile '{profileName}'.");
        }

        private static void HandleListCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winstasis list coding')");
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

            Console.WriteLine($"Profile: {profile.ProfileName} (Saved: {profile.CreatedAt})");
            Console.WriteLine(new string('-', 80));

            foreach (var win in profile.Windows)
            {
                // Format the workspace GUID into a readable short string if it exists
                string workspaceStr = win.DesktopId != Guid.Empty 
                    ? $"[WS: {win.DesktopId.ToString().Substring(0, 8)}...]" 
                    : "[WS: Global]";

                Console.WriteLine($"[{win.TargetId:D2}] {workspaceStr} {win.ProcessName}.exe - \"{win.WindowTitle}\"");
            }
            Console.WriteLine(new string('-', 80));
        }

        private static void HandleRestoreCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winstasis restore coding')");
                return;
            }

            string profileName = args[1];
            Console.WriteLine($"[TODO] Restoring profile: {profileName}...");
        }
    }
}