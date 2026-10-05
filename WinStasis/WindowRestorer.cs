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

        public void Restore(SessionProfile profile, int? targetId = null)
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

            // =====================================================================
            // PASS 1: FAST-PATH HWND & EXACT TITLE MATCH
            // =====================================================================
            foreach (var win in windowsToRestore)
            {
                long savedHwnd = win.Hwnd;

                // 1. Fast Path: Check if saved HWND is still valid and same process
                if (savedHwnd != 0 && !claimedHwnds.Contains(savedHwnd) && 
                    _env.IsWindowAlive(savedHwnd) && _env.IsWindowVisible(savedHwnd))
                {
                    if (_env.GetWindowProcessName(savedHwnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        claimedHwnds.Add(savedHwnd);
                        windowAssignments[win.TargetId] = savedHwnd;
                        continue;
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
                        // SAFETY GUARD: Require a non-empty title so we don't accidentally
                        // match the Windows Taskbar (Shell_TrayWnd) or hidden helper windows.
                        string liveTitle = _env.GetWindowTitle(hWnd);
                        if (string.IsNullOrWhiteSpace(liveTitle))
                            continue;

                        if (_env.GetWindowProcessName(hWnd).Equals(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            claimedHwnds.Add(hWnd);
                            windowAssignments[win.TargetId] = hWnd;
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
                WindowRect targetRect = new WindowRect(
                    win.X, 
                    win.Y, 
                    win.X + win.Width, 
                    win.Y + win.Height
                );
                targetRect = ClampToNearestMonitor(targetRect);

                // Placement & Contextual Override
                WindowPlacement placement = _env.GetWindowPlacement(hWnd);
                placement.NormalPosition = targetRect;
                placement.ShowCmd = win.ShowCmd;

                // ADR-0004: Contextual State Override
                if (targetId.HasValue && placement.ShowCmd == 2)
                {
                    placement.ShowCmd = 1;
                }

                bool result = _env.SetWindowPlacement(hWnd, placement);

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

        // =====================================================================
        // BOUNDARY CLAMPING (ADR-0003)
        // =====================================================================
        private WindowRect ClampToNearestMonitor(WindowRect targetRect)
        {
            WindowRect workArea = _env.GetWorkAreaForRect(targetRect);

            if (workArea.Left == targetRect.Left && workArea.Right == targetRect.Right && 
                workArea.Top == targetRect.Top && workArea.Bottom == targetRect.Bottom)
            {
                return targetRect;
            }

            int width = targetRect.Width;
            int height = targetRect.Height;

            bool isOutsideLeft = targetRect.Right <= workArea.Left;
            bool isOutsideRight = targetRect.Left >= workArea.Right;
            bool isOutsideTop = targetRect.Bottom <= workArea.Top;
            bool isOutsideBottom = targetRect.Top >= workArea.Bottom;

            if (isOutsideLeft || isOutsideRight || isOutsideTop || isOutsideBottom)
            {
                int newLeft = Math.Max(workArea.Left, Math.Min(targetRect.Left, workArea.Right - width));
                int newTop = Math.Max(workArea.Top, Math.Min(targetRect.Top, workArea.Bottom - height));
                
                return new WindowRect(newLeft, newTop, newLeft + width, newTop + height);
            }

            return targetRect;
        }
    }
}
