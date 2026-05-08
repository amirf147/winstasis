using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WinStasis.Models;

namespace WinStasis
{
    /// <summary>
    /// Handles the Phase 3 logic: Finding the correct window, clamping coordinates, and applying state.
    /// </summary>
    internal static class WindowRestorer
    {
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect([In] ref Program.RECT lprc, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref Program.WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public Program.RECT rcMonitor;
            public Program.RECT rcWork;
            public uint dwFlags;
        }

        // =====================================================================
        // 1. HYBRID MATCHING (ADR-0001)
        // =====================================================================
        public static IntPtr FindWindow(WindowRecord record)
        {
            IntPtr savedHwnd = (IntPtr)record.Hwnd;

            // Fast Path: Check if the saved HWND still exists and is visible
            if (IsWindow(savedHwnd) && Program.IsWindowVisible(savedHwnd))
            {
                // HWNDs can be recycled by the OS. We do a quick Process Name check to ensure 
                // we aren't moving a completely different app that stole this HWND.
                _ = Program.GetWindowThreadProcessId(savedHwnd, out uint processId);
                try
                {
                    using Process proc = Process.GetProcessById((int)processId);
                    if (proc.ProcessName.Equals(record.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        return savedHwnd; // 100% Match!
                    }
                }
                catch { /* Ignore access denied and fall back */ }
            }

            // Fallback Path: First-Come, First-Served match on Process Name + Window Title
            IntPtr foundHwnd = IntPtr.Zero;

            bool MatchWindowCallback(IntPtr hWnd, IntPtr lParam)
            {
                if (!Program.IsWindowVisible(hWnd)) return true;

                StringBuilder titleBuilder = new(256);
                _ = Program.GetWindowText(hWnd, titleBuilder, 256);
                string windowTitle = titleBuilder.ToString().Trim();

                if (windowTitle != record.WindowTitle) return true; // Title doesn't match

                _ = Program.GetWindowThreadProcessId(hWnd, out uint processId);
                try
                {
                    using Process proc = Process.GetProcessById((int)processId);
                    if (proc.ProcessName.Equals(record.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundHwnd = hWnd; // Found the match!
                        return false; // Stop EnumWindows early
                    }
                }
                catch { }

                return true; // Keep looking
            }

            Program.EnumWindows(MatchWindowCallback, IntPtr.Zero);
            return foundHwnd;
        }

        // =====================================================================
        // 2. BOUNDARY CLAMPING (ADR-0003)
        // =====================================================================
        public static Program.RECT ClampToNearestMonitor(Program.RECT targetRect)
        {
            // Find the nearest monitor to where the window *wants* to be
            IntPtr hMonitor = MonitorFromRect(ref targetRect, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return targetRect; // Safety fallback

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();

            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                Program.RECT workArea = monitorInfo.rcWork;
                int width = targetRect.Right - targetRect.Left;
                int height = targetRect.Bottom - targetRect.Top;

                // Check if the rectangle is entirely outside the visible working area
                bool isOutsideLeft = targetRect.Right <= workArea.Left;
                bool isOutsideRight = targetRect.Left >= workArea.Right;
                bool isOutsideTop = targetRect.Bottom <= workArea.Top;
                bool isOutsideBottom = targetRect.Top >= workArea.Bottom;

                if (isOutsideLeft || isOutsideRight || isOutsideTop || isOutsideBottom)
                {
                    // Shift the X and Y coordinates inside the visible boundaries
                    targetRect.Left = Math.Max(workArea.Left, Math.Min(targetRect.Left, workArea.Right - width));
                    targetRect.Top = Math.Max(workArea.Top, Math.Min(targetRect.Top, workArea.Bottom - height));
                    
                    targetRect.Right = targetRect.Left + width;
                    targetRect.Bottom = targetRect.Top + height;
                }
            }

            return targetRect;
        }
    }
}
