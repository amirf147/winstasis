using System;
using System.Collections.Generic;
using System.Linq;
using WinStasis.Interfaces;
using WinStasis.Models;

namespace WinStasis
{
    /// <summary>
    /// Handles the entire restore pipeline using the provided OS environment port.
    /// </summary>
    public class WindowRestorer
    {
        private readonly IWindowingEnvironment _env;

        public WindowRestorer(IWindowingEnvironment env)
        {
            _env = env;
        }

        public void Restore(SessionProfile profile, int? targetId = null, bool verbose = false)
        {
            var windowsToRestore = targetId.HasValue 
                ? profile.Windows.Where(w => w.TargetId == targetId.Value).ToList() 
                : profile.Windows.ToList();

            if (windowsToRestore.Count == 0)
            {
                Console.WriteLine($"Error: No window found with Target ID {targetId}.");
                return;
            }

            Console.WriteLine($"Restoring {(targetId.HasValue ? "target " + targetId : "all windows")} from profile '{profile.ProfileName}'...\n");

            var visibleWindows = _env.GetVisibleWindows().ToList();
            var claimedHwnds = new HashSet<long>();
            var windowAssignments = new Dictionary<int, long>(); // TargetId -> hWnd
            var matchReasons = new Dictionary<int, string>(); // TargetId -> Reason

            // =====================================================================
            // PASS 1: FAST-PATH HWND & EXACT TITLE MATCH
            // =====================================================================
            foreach (var win in windowsToRestore)
            {
                long savedHwnd = win.Hwnd;

                // 1. Fast Path: Check if saved HWND is still valid and an active application window
                if (savedHwnd != 0 && !claimedHwnds.Contains(savedHwnd) && 
                    _env.IsWindowAlive(savedHwnd) && _env.IsWindowVisible(savedHwnd) && _env.IsAppWindow(savedHwnd))
                {
                    if (_env.GetWindowProcessName(savedHwnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        string liveTitle = _env.GetWindowTitle(savedHwnd);
                        if (string.IsNullOrEmpty(win.WindowTitle) || !string.IsNullOrWhiteSpace(liveTitle))
                        {
                            claimedHwnds.Add(savedHwnd);
                            windowAssignments[win.TargetId] = savedHwnd;
                            matchReasons[win.TargetId] = "Pass 1 (Fast-Path HWND)";
                            continue;
                        }
                    }
                }

                // 2. Exact Match on Process Name + Window Title
                foreach (long hWnd in visibleWindows)
                {
                    if (!claimedHwnds.Contains(hWnd))
                    {
                        if (_env.GetWindowProcessName(hWnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                            _env.GetWindowTitle(hWnd) == win.WindowTitle)
                        {
                            claimedHwnds.Add(hWnd);
                            windowAssignments[win.TargetId] = hWnd;
                            matchReasons[win.TargetId] = "Pass 1 (Exact Process + Title)";
                            break;
                        }
                    }
                }
            }

            // =====================================================================
            // PASS 2: PROCESS FALLBACK (Title changed across reboots/updates)
            // =====================================================================
            foreach (var win in windowsToRestore)
            {
                if (windowAssignments.ContainsKey(win.TargetId))
                    continue;

                foreach (long hWnd in visibleWindows)
                {
                    if (!claimedHwnds.Contains(hWnd))
                    {
                        // SAFETY GUARDS:
                        // 1. Require a non-empty title so we don't accidentally
                        // match the Windows Taskbar (Shell_TrayWnd) or hidden helper windows.
                        string liveTitle = _env.GetWindowTitle(hWnd);
                        if (string.IsNullOrWhiteSpace(liveTitle))
                            continue;

                        // 2. Ensure it is a valid top-level application window
                        if (!_env.IsAppWindow(hWnd))
                            continue;

                        if (_env.GetWindowProcessName(hWnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            claimedHwnds.Add(hWnd);
                            windowAssignments[win.TargetId] = hWnd;
                            matchReasons[win.TargetId] = "Pass 2 (Process Fallback)";
                            break;
                        }
                    }
                }
            }

            // =====================================================================
            // EXECUTE WINDOW PLACEMENT & WORKSPACE ASSIGNMENT
            // =====================================================================
            int successCount = 0;
            int notFoundCount = 0;

            foreach (var win in windowsToRestore)
            {
                if (!windowAssignments.TryGetValue(win.TargetId, out long hWnd) || hWnd == 0)
                {
                    Console.WriteLine($"[Not Found] [{win.TargetId:D2}] {win.ProcessName}.exe - \"{win.WindowTitle}\"");
                    Console.WriteLine($"            -> Application is closed or no available window found.");
                    notFoundCount++;
                    continue;
                }

                WindowDiagnostics? preDiag = verbose ? _env.GetWindowDiagnostics(hWnd) : null;

                // Workspace Assignment
                if (win.IsPinned)
                {
                    _env.PinWindow(hWnd);
                }
                else
                {
                    if (_env.IsWindowPinned(hWnd))
                    {
                        _env.UnpinWindow(hWnd);
                    }

                    if (win.DesktopId != Guid.Empty)
                    {
                        _env.MoveWindowToDesktop(hWnd, win.DesktopId);
                    }
                }

                // Boundary Clamping
                WindowRect rawTargetRect = new WindowRect(
                    win.X, 
                    win.Y, 
                    win.X + win.Width, 
                    win.Y + win.Height
                );
                WindowRect targetRect = ClampToNearestMonitor(rawTargetRect);

                // Placement & Contextual Override
                WindowPlacement placement = _env.GetWindowPlacement(hWnd);

                // Detect corrupted minimized coordinates from pre-fix profiles (e.g. -32000, -32000 or placeholder icon box)
                bool isCorruptedMinimizedRect = win.X <= -10000 || win.Y <= -10000 || 
                    (win.ShowCmd == 2 && (win.Width < 200 || win.Height < 100));

                if (!isCorruptedMinimizedRect)
                {
                    placement.NormalPosition = targetRect;
                }
                else
                {
                    // Preserve existing live NormalPosition if valid, otherwise clamp
                    if (placement.NormalPosition.Width < 200 || placement.NormalPosition.Height < 100)
                    {
                        placement.NormalPosition = targetRect;
                    }
                }

                placement.ShowCmd = win.ShowCmd;

                // ADR-0004: Contextual State Override
                if (targetId.HasValue && placement.ShowCmd == 2)
                {
                    placement.ShowCmd = 1;
                }

                // Step 1: SetWindowPlacement
                bool result = _env.SetWindowPlacement(hWnd, placement);

                WindowFrameMargins margins = default;
                WindowRect compensatedWin32Rect = default;

                // Steps 2 & 3: DWM Frame Margin Expansion & SetWindowPosition
                if (result && win.ShowCmd == 1 && !isCorruptedMinimizedRect)
                {
                    margins = _env.GetFrameMargins(hWnd);
                    compensatedWin32Rect = new WindowRect(
                        targetRect.Left - margins.Left,
                        targetRect.Top - margins.Top,
                        targetRect.Right + margins.Right,
                        targetRect.Bottom + margins.Bottom
                    );
                    _env.SetWindowPosition(hWnd, compensatedWin32Rect);
                }

                if (result)
                {
                    Console.WriteLine($"[Restored]  [{win.TargetId:D2}] {win.ProcessName}.exe");
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"[Failed]    [{win.TargetId:D2}] {win.ProcessName}.exe");
                }

                if (verbose && preDiag != null)
                {
                    string matchReason = matchReasons.TryGetValue(win.TargetId, out var reason) ? reason : "Unknown";
                    Console.WriteLine($"    Match: {matchReason} -> HWND 0x{hWnd:X} (Class: \"{preDiag.ClassName}\", Title: \"{_env.GetWindowTitle(hWnd)}\")");
                    Console.WriteLine($"    Styles: Style=0x{preDiag.Style:X8}, ExStyle=0x{preDiag.ExStyle:X8}");
                    Console.WriteLine($"    Pre-Restore Bounds: Win32=({preDiag.RawWin32Rect.Left},{preDiag.RawWin32Rect.Top} {preDiag.RawWin32Rect.Width}x{preDiag.RawWin32Rect.Height}) | DWM=({preDiag.VisibleDwmRect.Left},{preDiag.VisibleDwmRect.Top} {preDiag.VisibleDwmRect.Width}x{preDiag.VisibleDwmRect.Height})");
                    Console.WriteLine($"    Target Clamping: Saved=({rawTargetRect.Left},{rawTargetRect.Top} {rawTargetRect.Width}x{rawTargetRect.Height}) -> Clamped=({targetRect.Left},{targetRect.Top} {targetRect.Width}x{targetRect.Height})");

                    if (win.ShowCmd == 1 && !isCorruptedMinimizedRect)
                    {
                        Console.WriteLine($"    DWM Compensation: Margins(L={margins.Left}, T={margins.Top}, R={margins.Right}, B={margins.Bottom}) -> Applied Win32=({compensatedWin32Rect.Left},{compensatedWin32Rect.Top} {compensatedWin32Rect.Width}x{compensatedWin32Rect.Height})");
                    }
                }
            }

            Console.WriteLine($"\nRestore complete: {successCount} restored, {notFoundCount} missing.");
        }

        // =====================================================================
        // BOUNDARY CLAMPING (ADR-0003)
        // =====================================================================
        private WindowRect ClampToNearestMonitor(WindowRect targetRect)
        {
            WindowRect workArea = _env.GetWorkAreaForRect(targetRect);

            if (workArea.Width <= 0 || workArea.Height <= 0)
                return targetRect;

            if (targetRect.Left < workArea.Left || targetRect.Right > workArea.Right ||
                targetRect.Top < workArea.Top || targetRect.Bottom > workArea.Bottom)
            {
                int width = Math.Min(targetRect.Width, workArea.Width);
                int height = Math.Min(targetRect.Height, workArea.Height);
                int newLeft = Math.Clamp(targetRect.Left, workArea.Left, workArea.Right - width);
                int newTop = Math.Clamp(targetRect.Top, workArea.Top, workArea.Bottom - height);
                return new WindowRect(newLeft, newTop, newLeft + width, newTop + height);
            }

            return targetRect;
        }
    }
}
