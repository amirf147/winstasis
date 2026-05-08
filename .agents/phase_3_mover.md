# Phase 3: The Mover (In Progress - BLOCKED)
**Goal:** Read the saved JSON profile, find the exact windows across the OS, apply safety constraints, and physically move them to their saved coordinates and workspaces.

## 1. Achievements
* Implemented **Hybrid Window Matching** (`WindowRestorer.FindWindow`). Successfully handles intra-session drift and OS `HWND` recycling.
* Implemented **Boundary Clamping** (`WindowRestorer.ClampToNearestMonitor`). Unmanaged Win32 `MonitorFromRect` successfully calculates safe coordinates for docked/undocked multi-monitor laptops.
* Connected `VirtualDesktopHelper` to Mover logic. **[CRITICAL BUG]**: Official COM API fails with `0x80070005` for cross-process moves.
* Implemented `winstasis restore <profile>` (Bulk restore) and `winstasis restore <profile> --target X` (Single target).
* Implemented **Contextual State Override**. If a single target was saved as "Minimized", the restore wakes it up to "Normal" automatically.
* Opaque Window Rule strictly enforced (closed apps correctly print a `[Not Found]` message rather than crashing or attempting a hacky `.exe` launch).

## 2. Next Steps (Blocked)
1. **Transition to Undocumented API:** Implement `IVirtualDesktopManagerInternal` to bypass the cross-process move restriction (ADR-0007).
2. **Handle OS Versioning:** Ensure the internal interface GUIDs are dynamically selected based on the Windows build (e.g., 22H2 vs 24H2).
3. **Verify:** Test restoration across multiple virtual desktops with cross-process windows.
