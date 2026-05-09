# Research Spike: Managing Virtual Desktops via C#

**Status:** Research / Paused Development

## 1. The Core Problem
Windows 10 and 11 introduced Virtual Desktops (Workspaces). While Microsoft provides official COM interfaces for *reading* which desktop a window is on (`IVirtualDesktopManager::GetWindowDesktopId`), they intentionally restrict the ability to *move* windows across desktops. The official `MoveWindowToDesktop` API returns an `Access Denied (0x80070005)` error if a process attempts to move a window it does not explicitly own.

To orchestrate workspaces globally, developers must hook into the `ImmersiveShell` and use the undocumented `IVirtualDesktopManagerInternal` COM interface.

## 2. The Maintenance Trap
Because `IVirtualDesktopManagerInternal` is undocumented, Microsoft does not guarantee backward compatibility. In almost every major Windows release (e.g., Windows 10 21H2, Windows 11 22H2, 23H2), Microsoft alters the internal memory layout of this interface.

If a C# `P/Invoke` application defines the interface with the incorrect method order or memory offset, invoking a method will result in an `AccessViolationException`, instantly crashing the application.

## 3. Existing 3rd-Party Solutions (Libraries)
To solve this, the open-source community maintains libraries that sniff the user's OS build number at runtime and dynamically load the correct memory offsets.

### Slion's Fork (`VirtualDesktop` for C#)
*   **Repository:** `Slion/VirtualDesktop`
*   **Can it list programs per desktop?** It does not have a built-in `GetAllWindowsForDesktop()` command. It provides `VirtualDesktop.FromHwnd(hwnd)`, meaning a developer still has to write their own loop to find all open windows across the OS, and then ask the library "Which desktop is this window on?" 
*   **Verdict:** It is a developer toolkit, not an end-user CLI application.

### `pyvda` (Python Virtual Desktop Accessor)
*   **Repository:** `mrob95/pyvda`
*   **Can it list programs per desktop?** Yes, it provides an `AppView` object that can tell you a window's target desktop, and it has an `AppView.current()` method. 
*   **Verdict:** Like Slion's work, `pyvda` is a *developer library*, not a standalone product. Furthermore, because it is written in Python, users have to install Python, run `pip install pyvda`, and write their own script. It also suffers from the exact same "Windows Update Breakage" problem because it relies on the same undocumented COM memory offsets under the hood.

## 4. Does our Offshoot Product already exist?
**No.** There are *libraries* that allow developers to piece this information together, but there is no widely adopted, zero-dependency, native standalone CLI tool designed specifically for end-users to just type a command and get a verbose map of their Virtual Desktops and windows. Our offshoot fills the gap between "heavy developer libraries" and "basic classic tools like WinSpy that don't understand workspaces."
