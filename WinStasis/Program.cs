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

namespace WinStasis
{
    class Program
    {
        // =====================================================================
        // WIN32 API IMPORTS (P/Invoke)
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

                Guid desktopId = desktopHelper.GetWindowDesktopId(hWnd);
                bool isPinned = desktopHelper.IsWindowPinned(hWnd);

                capturedWindows.Add(new WindowRecord
                {
                    TargetId = currentTargetId++,
                    Hwnd = hWnd.ToInt64(),
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Right - rect.Left,
                    Height = rect.Bottom - rect.Top,
                    ShowCmd = placement.showCmd,
                    DesktopId = desktopId,
                    IsPinned = isPinned
                });

                return true;
            }

            EnumWindows(CaptureWindowCallback, IntPtr.Zero);

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
                Console.WriteLine("Error: Please provide a profile name. (e.g., 'winstasis restore coding')");
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

            var windowsToRestore = targetId.HasValue 
                ? profile.Windows.Where(w => w.TargetId == targetId.Value).ToList() 
                : profile.Windows.ToList();

            if (windowsToRestore.Count == 0)
            {
                Console.WriteLine($"Error: No window found with Target ID {targetId}.");
                return;
            }

            Console.WriteLine($"Restoring {(targetId.HasValue ? "target " + targetId : "all windows")} from profile '{profileName}'...\n");

            var desktopHelper = new VirtualDesktopHelper();
            int successCount = 0;
            int notFoundCount = 0;

            foreach (var win in windowsToRestore)
            {
                // 1. Hybrid Matching: Find the alive window handle
                IntPtr hWnd = WindowRestorer.FindWindow(win);

                if (hWnd == IntPtr.Zero)
                {
                    Console.WriteLine($"[Not Found] [{win.TargetId:D2}] {win.ProcessName}.exe - \"{win.WindowTitle}\"");
                    Console.WriteLine($"            -> Application is closed or title changed. (Skipped per Opaque Window Rule)");
                    notFoundCount++;
                    continue;
                }

                // 2. Workspace Assignment: Handle Pinned Windows and Desktop moves
                if (win.IsPinned)
                {
                    desktopHelper.PinWindow(hWnd);
                }
                else
                {
                    // If it shouldn't be pinned but currently is, unpin it first
                    if (desktopHelper.IsWindowPinned(hWnd))
                    {
                        desktopHelper.UnpinWindow(hWnd);
                    }

                    if (win.DesktopId != Guid.Empty)
                    {
                        desktopHelper.MoveWindowToDesktop(hWnd, win.DesktopId);
                    }
                }

                // 3. Boundary Clamping: Make sure coordinates are safe for current screens
                RECT targetRect = new RECT
                {
                    Left = win.X,
                    Top = win.Y,
                    Right = win.X + win.Width,
                    Bottom = win.Y + win.Height
                };
                targetRect = WindowRestorer.ClampToNearestMonitor(targetRect);

                // 4. Extract current placement so we don't destroy other flags
                WINDOWPLACEMENT placement = new() { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                GetWindowPlacement(hWnd, ref placement);

                // 5. Apply new coords and Contextual State Override
                placement.rcNormalPosition = targetRect;
                placement.showCmd = win.ShowCmd;

                // ADR-0004: If the user targeted a single window, but it was minimized (showCmd 2), force it to Normal (1)
                if (targetId.HasValue && placement.showCmd == 2)
                {
                    placement.showCmd = 1;
                }

                // 6. Execute the move
                bool result = WindowRestorer.SetWindowPlacement(hWnd, ref placement);

                if (result)
                {
                    Console.WriteLine($"[Restored]  [{win.TargetId:D2}] {win.ProcessName}.exe");
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"[Failed]    [{win.TargetId:D2}] {win.ProcessName}.exe");
                }
            }

            Console.WriteLine($"\nRestore complete: {successCount} restored, {notFoundCount} missing.");
        }
    }
}