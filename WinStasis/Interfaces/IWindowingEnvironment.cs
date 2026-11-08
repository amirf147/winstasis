using System;
using System.Collections.Generic;
using WinStasis.Models;

namespace WinStasis.Interfaces
{
    public interface IWindowingEnvironment
    {
        // Visibility and Enumeration
        bool IsWindowAlive(long hwnd);
        bool IsWindowVisible(long hwnd);
        string GetWindowTitle(long hwnd);
        string GetWindowProcessName(long hwnd);
        bool IsAppWindow(long hwnd);
        IEnumerable<long> GetVisibleWindows();

        // Identity and Container Introspection
        long GetWindowInstanceToken(long hwnd);
        void SetWindowInstanceToken(long hwnd, long token);
        string[] GetWindowTabs(long hwnd);

        // Virtual Desktop & Pinning
        Guid GetWindowDesktopId(long hwnd);
        int GetDesktopNumber(Guid desktopId);
        bool IsWindowPinned(long hwnd);
        void PinWindow(long hwnd);
        void UnpinWindow(long hwnd);
        bool MoveWindowToDesktop(long hwnd, Guid targetDesktopId);

        // Geometry and State
        WindowRect GetWorkAreaForRect(WindowRect targetRect);
        WindowPlacement GetWindowPlacement(long hwnd);
        bool SetWindowPlacement(long hwnd, WindowPlacement placement);
        WindowRect GetWindowBounds(long hwnd);
        WindowRect GetVisibleWindowBounds(long hwnd);
        WindowFrameMargins GetFrameMargins(long hwnd);
        bool SetWindowPosition(long hwnd, WindowRect win32Rect);
        WindowDiagnostics GetWindowDiagnostics(long hwnd);
    }
}
