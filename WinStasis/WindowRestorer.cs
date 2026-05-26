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

            int successCount = 0;
            int notFoundCount = 0;

            foreach (var win in windowsToRestore)
            {
                long hWnd = FindWindow(win);

                if (hWnd == 0)
                {
                    Console.WriteLine($"[Not Found] [{win.TargetId:D2}] {win.ProcessName}.exe - \"{win.WindowTitle}\"");
                    Console.WriteLine($"            -> Application is closed or title changed. (Skipped per Opaque Window Rule)");
                    notFoundCount++;
                    continue;
                }

                // 2. Workspace Assignment
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

                // 3. Boundary Clamping
                WindowRect targetRect = new WindowRect(
                    win.X, 
                    win.Y, 
                    win.X + win.Width, 
                    win.Y + win.Height
                );
                targetRect = ClampToNearestMonitor(targetRect);

                // 4. Placement & Contextual Override
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
        // 1. HYBRID MATCHING (ADR-0001)
        // =====================================================================
        private long FindWindow(WindowRecord record)
        {
            long savedHwnd = record.Hwnd;

            // Fast Path: Check if the saved HWND still exists and is visible
            if (_env.IsWindowAlive(savedHwnd) && _env.IsWindowVisible(savedHwnd))
            {
                // Verify process name hasn't changed (recycled HWND check)
                if (_env.GetWindowProcessName(savedHwnd).Equals(record.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return savedHwnd; // 100% Match!
                }
            }

            // Fallback Path: First-Come, First-Served match on Process Name + Window Title
            foreach (long hWnd in _env.GetVisibleWindows())
            {
                if (_env.GetWindowTitle(hWnd) == record.WindowTitle)
                {
                    if (_env.GetWindowProcessName(hWnd).Equals(record.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        return hWnd;
                    }
                }
            }

            return 0; // Not found
        }

        // =====================================================================
        // 2. BOUNDARY CLAMPING (ADR-0003)
        // =====================================================================
        private WindowRect ClampToNearestMonitor(WindowRect targetRect)
        {
            WindowRect workArea = _env.GetWorkAreaForRect(targetRect);

            // If work area is identical to target, no clamping is needed.
            // (Or if the adapter failed and returned the original rect).
            if (workArea.Left == targetRect.Left && workArea.Right == targetRect.Right && 
                workArea.Top == targetRect.Top && workArea.Bottom == targetRect.Bottom)
            {
                return targetRect;
            }

            int width = targetRect.Width;
            int height = targetRect.Height;

            // Check if the rectangle is entirely outside the visible working area
            bool isOutsideLeft = targetRect.Right <= workArea.Left;
            bool isOutsideRight = targetRect.Left >= workArea.Right;
            bool isOutsideTop = targetRect.Bottom <= workArea.Top;
            bool isOutsideBottom = targetRect.Top >= workArea.Bottom;

            if (isOutsideLeft || isOutsideRight || isOutsideTop || isOutsideBottom)
            {
                // Shift the X and Y coordinates inside the visible boundaries
                int newLeft = Math.Max(workArea.Left, Math.Min(targetRect.Left, workArea.Right - width));
                int newTop = Math.Max(workArea.Top, Math.Min(targetRect.Top, workArea.Bottom - height));
                
                return new WindowRect(newLeft, newTop, newLeft + width, newTop + height);
            }

            return targetRect;
        }
    }
}
