# 6. Omniscient Workspace Tracking & Fallback

**Status:** Accepted

## Context
When running `EnumWindows`, the Windows API returns all top-level windows across *all* Virtual Desktops. If we do not explicitly track which desktop a window belongs to, restoring a session will "mash" all windows onto the current active desktop, defeating the purpose of multi-workspace management. 

We need a way to track desktops without relying heavily on undocumented Windows internal APIs that break during OS updates.

## Decision
We will use an **Omniscient Workspace Strategy** powered by official COM interfaces:
1. **Extraction:** Use the official `IVirtualDesktopManager::GetWindowDesktopId` to extract the unique GUID of the workspace for each window.
2. **Readability:** Parse the Windows Registry (`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops`) to map these GUIDs to human-readable Desktop Numbers (Desktop 1, Desktop 2) for the CLI list command.
3. **Restoration:** Use `MoveWindowToDesktop(HWND, GUID)`. 
4. **Fallback (Option B):** If a user reboots and Windows "forgets" extra workspaces, restoring to a missing GUID will fail. If `winst` detects a missing workspace GUID, it will fall back to restoring the window onto the current active desktop (salvaging the window).

*Future Proofing:* The codebase will be structured so that Option A (Undocumented Auto-Creation of missing workspaces) can be slotted in at a later date if the user accepts the maintenance burden.

## Consequences
* **Positive:** Accurate 1:1 restoration of multi-workspace layouts.
* **Positive:** Resilient against OS updates by relying on official GUID tracking rather than undocumented arrays.
* **Negative:** Requires interacting with complex COM interfaces via C# P/Invoke.
