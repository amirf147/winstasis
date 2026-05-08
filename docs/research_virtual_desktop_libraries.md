# Research Spike: Managing Virtual Desktops via C#

**Status:** Research / Paused Development

## 1. The Core Problem
Windows 10 and 11 introduced Virtual Desktops (Workspaces). While Microsoft provides official COM interfaces for *reading* which desktop a window is on (`IVirtualDesktopManager::GetWindowDesktopId`), they intentionally restrict the ability to *move* windows across desktops. The official `MoveWindowToDesktop` API returns an `Access Denied (0x80070005)` error if a process attempts to move a window it does not explicitly own.

To orchestrate workspaces globally, developers must hook into the `ImmersiveShell` and use the undocumented `IVirtualDesktopManagerInternal` COM interface.

## 2. The Maintenance Trap
Because `IVirtualDesktopManagerInternal` is undocumented, Microsoft does not guarantee backward compatibility. In almost every major Windows release (e.g., Windows 10 21H2, Windows 11 22H2, 23H2), Microsoft alters the internal memory layout of this interface.

If a C# `P/Invoke` application defines the interface with the incorrect method order or memory offset, invoking a method will result in an `AccessViolationException`, instantly crashing the application.

## 3. The 3rd-Party Solution: `VirtualDesktop`
To solve this, the open-source community maintains libraries that sniff the user's OS build number at runtime and dynamically load the correct memory offsets.

### Grabacr07's Original Library
*   **Repository:** `Grabacr07/VirtualDesktop`
*   **Significance:** The original pioneer in reverse-engineering the Windows 10 Virtual Desktop COM interfaces for C#. 
*   **Status:** Largely inactive. It struggles to keep pace with the rapid release cycle of Windows 11 updates.

### Slion's Fork (`VirtualDesktop`)
*   **Repository:** [Slion/VirtualDesktop](https://github.com/Slion/VirtualDesktop)
*   **Clarification on Capabilities:** To address the concern—**Yes, this library absolutely allows moving windows to specific workspaces.** Its core feature is exposing a `window.MoveToDesktop(desktop)` method that bypasses the OS `Access Denied` restriction by hooking into the `ImmersiveShell`. 
*   **What it does NOT do:** The library is *not* a layout manager. It does not save or restore sessions on its own. It simply provides the low-level API commands (like `MoveToDesktop` and `GetDesktops`) that `winstasis` needs to execute a cross-workspace move.
*   **How it works:** Slion's library uses dynamic COM instantiation and massive version-checking switch statements to map exact Windows Build numbers to the correct internal C++ interface definitions.


## 4. Architectural Implications for WinStasis
If `winstasis` requires automated cross-workspace restoration, it must abandon the goal of being a purely dependency-free, self-contained native script. 

**Trade-offs of adopting Slion/VirtualDesktop:**
*   **Pros:** Achieves the "Holy Grail" of workspace orchestration without the user having to drag windows manually. It is actively maintained and supports modern Windows 11 builds.
*   **Cons:** Introduces a heavy, volatile dependency. If Microsoft releases a new OS build and Slion has not yet updated the repository to map the new memory offsets, `winstasis` might fail or crash upon execution until the upstream dependency is patched.

