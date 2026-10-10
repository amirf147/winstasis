using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WinStasis.Interfaces;
using WinStasis.Models;

namespace WinStasis.Adapters
{
    public class WindowsEnvironmentAdapter : IWindowingEnvironment
    {
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private readonly VirtualDesktopHelper _desktopHelper;

        public WindowsEnvironmentAdapter()
        {
            _desktopHelper = new VirtualDesktopHelper();
        }

        public bool IsWindowAlive(long hwnd) => IsWindow((IntPtr)hwnd);

        public bool IsWindowVisible(long hwnd) => IsWindowVisible((IntPtr)hwnd);

        public string GetWindowTitle(long hwnd)
        {
            StringBuilder titleBuilder = new(256);
            _ = GetWindowText((IntPtr)hwnd, titleBuilder, 256);
            return titleBuilder.ToString().Trim();
        }

        public string GetWindowProcessName(long hwnd)
        {
            _ = GetWindowThreadProcessId((IntPtr)hwnd, out uint processId);
            try
            {
                using Process proc = Process.GetProcessById((int)processId);
                return proc.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool IsAppWindow(long hwnd)
        {
            IntPtr hWnd = (IntPtr)hwnd;
            if (!IsWindow(hWnd) || !IsWindowVisible(hWnd))
                return false;

            long exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;

            if (isToolWindow && !isAppWindow)
                return false;

            IntPtr owner = GetWindow(hWnd, GW_OWNER);
            if (owner != IntPtr.Zero && !isAppWindow)
                return false;

            return true;
        }

        public IEnumerable<long> GetVisibleWindows()
        {
            var windows = new List<long>();
            bool EnumCallback(IntPtr hWnd, IntPtr lParam)
            {
                if (IsAppWindow(hWnd.ToInt64()))
                {
                    windows.Add(hWnd.ToInt64());
                }
                return true; // Keep enumerating
            }
            EnumWindows(EnumCallback, IntPtr.Zero);
            return windows;
        }

        public bool IsWindowPinned(long hwnd) => _desktopHelper.IsWindowPinned((IntPtr)hwnd);

        public void PinWindow(long hwnd) => _desktopHelper.PinWindow((IntPtr)hwnd);

        public void UnpinWindow(long hwnd) => _desktopHelper.UnpinWindow((IntPtr)hwnd);

        public bool MoveWindowToDesktop(long hwnd, Guid targetDesktopId) => 
            _desktopHelper.MoveWindowToDesktop((IntPtr)hwnd, targetDesktopId);

        public WindowRect GetWorkAreaForRect(WindowRect targetRect)
        {
            NativeRect nativeTarget = new NativeRect
            {
                Left = targetRect.Left,
                Top = targetRect.Top,
                Right = targetRect.Right,
                Bottom = targetRect.Bottom
            };

            IntPtr hMonitor = MonitorFromRect(ref nativeTarget, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return targetRect;

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();

            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return new WindowRect(
                    monitorInfo.rcWork.Left,
                    monitorInfo.rcWork.Top,
                    monitorInfo.rcWork.Right,
                    monitorInfo.rcWork.Bottom
                );
            }

            return targetRect; // fallback
        }

        public WindowPlacement GetWindowPlacement(long hwnd)
        {
            NATIVE_WINDOWPLACEMENT nativePlacement = new() { length = Marshal.SizeOf<NATIVE_WINDOWPLACEMENT>() };
            GetWindowPlacement((IntPtr)hwnd, ref nativePlacement);

            return new WindowPlacement
            {
                ShowCmd = nativePlacement.showCmd,
                NormalPosition = new WindowRect(
                    nativePlacement.rcNormalPosition.Left,
                    nativePlacement.rcNormalPosition.Top,
                    nativePlacement.rcNormalPosition.Right,
                    nativePlacement.rcNormalPosition.Bottom
                )
            };
        }

        public bool SetWindowPlacement(long hwnd, WindowPlacement placement)
        {
            // We need to fetch the current placement to avoid overwriting other fields (flags, min/max pos)
            NATIVE_WINDOWPLACEMENT nativePlacement = new() { length = Marshal.SizeOf<NATIVE_WINDOWPLACEMENT>() };
            GetWindowPlacement((IntPtr)hwnd, ref nativePlacement);

            nativePlacement.showCmd = placement.ShowCmd;
            nativePlacement.rcNormalPosition = new NativeRect
            {
                Left = placement.NormalPosition.Left,
                Top = placement.NormalPosition.Top,
                Right = placement.NormalPosition.Right,
                Bottom = placement.NormalPosition.Bottom
            };

            return SetWindowPlacement((IntPtr)hwnd, ref nativePlacement);
        }

        // =====================================================================
        // WIN32 INTEROP (PRIVATE TO ADAPTER)
        // =====================================================================

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private const int GWL_EXSTYLE = -20;
        private const uint GW_OWNER = 4;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref NATIVE_WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref NATIVE_WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect([In] ref NativeRect lprc, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NATIVE_WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public NativePoint ptMinPosition;
            public NativePoint ptMaxPosition;
            public NativeRect rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
        }
    }
}
