using System;
using System.Collections.Generic;
using WinStasis.Interfaces;
using WinStasis.Models;

namespace WinStasis
{
    public class WindowCapturer
    {
        private readonly IWindowingEnvironment _env;

        public WindowCapturer(IWindowingEnvironment env)
        {
            _env = env;
        }

        public SessionProfile Capture(string profileName, bool verbose = false)
        {
            var capturedWindows = new List<WindowRecord>();
            int currentTargetId = 1;

            foreach (long hwnd in _env.GetVisibleWindows())
            {
                string windowTitle = _env.GetWindowTitle(hwnd).Trim();
                if (string.IsNullOrEmpty(windowTitle))
                    continue;

                string processName = _env.GetWindowProcessName(hwnd);
                if (string.IsNullOrEmpty(processName))
                    processName = "Unknown";

                WindowPlacement placement = _env.GetWindowPlacement(hwnd);

                int x, y, width, height;

                if (placement.ShowCmd == 2) // Minimized
                {
                    x = placement.NormalPosition.Left;
                    y = placement.NormalPosition.Top;
                    width = placement.NormalPosition.Width;
                    height = placement.NormalPosition.Height;
                }
                else if (placement.ShowCmd == 3) // Maximized
                {
                    x = placement.NormalPosition.Left;
                    y = placement.NormalPosition.Top;
                    width = placement.NormalPosition.Width;
                    height = placement.NormalPosition.Height;

                    WindowRect normWorkArea = _env.GetWorkAreaForRect(placement.NormalPosition);
                    WindowRect activeWorkArea = _env.GetWorkAreaForRect(_env.GetWindowBounds(hwnd));

                    if (normWorkArea.Left != activeWorkArea.Left || normWorkArea.Top != activeWorkArea.Top)
                    {
                        int offsetX = placement.NormalPosition.Left - normWorkArea.Left;
                        int offsetY = placement.NormalPosition.Top - normWorkArea.Top;
                        int maxLeft = Math.Max(activeWorkArea.Left, activeWorkArea.Right - width);
                        int maxTop = Math.Max(activeWorkArea.Top, activeWorkArea.Bottom - height);
                        x = Math.Clamp(activeWorkArea.Left + offsetX, activeWorkArea.Left, maxLeft);
                        y = Math.Clamp(activeWorkArea.Top + offsetY, activeWorkArea.Top, maxTop);
                    }
                }
                else // ShowCmd == 1 (Normal / Snapped, or default)
                {
                    WindowRect visibleBounds = _env.GetVisibleWindowBounds(hwnd);
                    x = visibleBounds.Left;
                    y = visibleBounds.Top;
                    width = visibleBounds.Width;
                    height = visibleBounds.Height;
                }

                if (width <= 0 || height <= 0)
                    continue;

                Guid desktopId = _env.GetWindowDesktopId(hwnd);
                bool isPinned = _env.IsWindowPinned(hwnd);
                int targetId = currentTargetId++;

                if (verbose)
                {
                    WindowDiagnostics diag = _env.GetWindowDiagnostics(hwnd);
                    Console.WriteLine($"  [Capture] [{targetId:D2}] {processName}.exe (HWND: 0x{hwnd:X}, Class: \"{diag.ClassName}\", ShowCmd: {placement.ShowCmd}) - \"{windowTitle}\"");
                    Console.WriteLine($"    Styles: Style=0x{diag.Style:X8}, ExStyle=0x{diag.ExStyle:X8}");
                    Console.WriteLine($"    Bounds: Win32=({diag.RawWin32Rect.Left},{diag.RawWin32Rect.Top} {diag.RawWin32Rect.Width}x{diag.RawWin32Rect.Height}) | DWM=({diag.VisibleDwmRect.Left},{diag.VisibleDwmRect.Top} {diag.VisibleDwmRect.Width}x{diag.VisibleDwmRect.Height}) | NormalPos=({diag.NormalPositionRect.Left},{diag.NormalPositionRect.Top} {diag.NormalPositionRect.Width}x{diag.NormalPositionRect.Height})");
                    Console.WriteLine($"    Margins: L={diag.FrameMargins.Left}, T={diag.FrameMargins.Top}, R={diag.FrameMargins.Right}, B={diag.FrameMargins.Bottom} -> Saved=({x},{y} {width}x{height})");
                }

                capturedWindows.Add(new WindowRecord
                {
                    TargetId = targetId,
                    Hwnd = hwnd,
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

            return new SessionProfile
            {
                ProfileName = profileName,
                CreatedAt = DateTime.Now,
                Windows = capturedWindows.ToArray()
            };
        }
    }
}
