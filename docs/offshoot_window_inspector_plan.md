# Offshoot Plan: Verbose Window Inspector CLI

**Status:** Proposed Offshoot Project

## 1. The Vision
A lightweight, dependency-free, read-only command-line utility that scans, maps, and logs every visible window on a Windows system. 

Instead of trying to fight the Windows operating system by forcibly moving windows across virtual desktops (which requires fragile, undocumented APIs), this tool fully embraces the "Unix Philosophy": **Do one thing, and do it well.** It acts as a highly detailed "top" or "ls" command specifically for the Windows desktop UI layer.

## 2. Why this is perfect for "Tiny Tool Town"
*   **Zero Dependencies:** Because it only *reads* data, it relies entirely on official, stable Microsoft Win32 and COM APIs. No third-party packages, no memory hacks, no breaking on Windows Updates.
*   **Highly Reliable:** It will work exactly the same on Windows 10 and Windows 11.
*   **Composable:** By outputting clean JSON, CSV, or formatted text, other scripts and tools can consume its output to do their own logic.

## 3. Core Requirements & Features
The tool will execute a scan and output a verbose payload for every visible window.

**Data Points Captured:**
*   **Identity:** Window Handle (`HWND`), Process ID (`PID`), Process Name (`.exe`), Window Title.
*   **Geometry:** X/Y Coordinates, Width/Height, Screen Bounds.
*   **Visual State:** Normal, Minimized, Maximized.
*   **Topology:** Which physical monitor the window is currently rendering on.
*   **Virtual Workspace:** The specific Virtual Desktop GUID the window belongs to (and translating that to a readable "Desktop 1", "Desktop 2" format).

## 4. Potential Commands
*   `winspy list` - Prints a clean, human-readable table of all windows and their workspaces to the console.
*   `winspy export --format json` - Dumps the raw data into a structured JSON file for auditing or piping into other pipeline tools.
*   `winspy inspect <PID>` - Returns deep, verbose information about a single specific application.

## 5. Architectural Takeaways from WinStasis
We have already solved the hardest parts of this offshoot during the `winstasis` Phase 1 and 2 spikes:
1.  We know how to filter out "invisible" Windows background junk using `IsWindowVisible`.
2.  We know how to map PIDs to process names safely.
3.  We know how to successfully extract Workspace GUIDs using the official `IVirtualDesktopManager` COM interface.

By stripping out the "Mover" (Phase 3) logic from WinStasis, this offshoot is already 80% complete and highly stable.
