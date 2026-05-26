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
        IEnumerable<long> GetVisibleWindows();

        // Virtual Desktop & Pinning
        bool IsWindowPinned(long hwnd);
        void PinWindow(long hwnd);
        void UnpinWindow(long hwnd);
        bool MoveWindowToDesktop(long hwnd, Guid targetDesktopId);

        // Geometry and State
        WindowRect GetWorkAreaForRect(WindowRect targetRect);
        WindowPlacement GetWindowPlacement(long hwnd);
        bool SetWindowPlacement(long hwnd, WindowPlacement placement);
    }
}
