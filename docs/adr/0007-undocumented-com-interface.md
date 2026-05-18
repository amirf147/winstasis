# 7. Undocumented COM Interface for Cross-Process Workspace Moves

**Status:** Accepted

## Context
During Phase 3 testing, we discovered a hidden security restriction in the official `IVirtualDesktopManager::MoveWindowToDesktop` API: it returns `0x80070005` (Access Denied) when attempting to move a window owned by a different process, even when running as an Administrator. This completely breaks `winst`'s ability to restore cross-workspace layouts.

The tool correctly *extracts* the GUIDs using the official API, but fails when trying to *apply* them.

## Decision
We must use the undocumented `IVirtualDesktopManagerInternal` COM interface. This is the internal Windows 11 system API used by `explorer.exe` (the Taskbar/Task View) to orchestrate workspaces. Because it is an internal system interface, it bypasses the "own-process-only" restriction.

We will write a C# COM Wrapper that attempts to instantiate the `IServiceProvider` for the `ImmersiveShell`, and request the `IVirtualDesktopManagerInternal` instance.

## Consequences
* **Positive:** Unlocks the core requirement of throwing any window to any workspace.
* **Negative:** High fragility. Microsoft historically changes the GUIDs and function signatures of `IVirtualDesktopManagerInternal` in minor OS updates. We will have to implement version-checking or catch exceptions gracefully, falling back to the current active workspace if the undocumented COM call fails.
