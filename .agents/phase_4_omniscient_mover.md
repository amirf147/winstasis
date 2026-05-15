# Phase 4: Omniscient Mover (Completed)
**Goal:** Achieve the "Holy Grail" of programmatic cross-workspace window movement, bypassing the official Windows API's `Access Denied` restrictions.

## 1. Achievements
* **Dependency Integration:** Successfully imported the `Slions.VirtualDesktop` package to handle the volatile, undocumented `IVirtualDesktopManagerInternal` memory offsets safely.
* **Target Framework Update:** Shifted the project to `net10.0-windows10.0.19041.0` to support modern Windows SDK COM wrappers required by the dependency.
* **Threading Fix:** Added the `[STAThread]` attribute to `Program.cs` to satisfy the COM apartment requirements for the Immersive Shell UI components, preventing silent runtime crashes.
* **Cross-Workspace Moves:** Replaced the failing manual `MoveWindowToDesktop` API call with `VirtualDesktop.MoveToDesktop()`, achieving perfect 1:1 cross-workspace restoration.

## 2. Known Constraints (OS Level)
* **UIPI (User Interface Privilege Isolation):** Windows correctly blocks `winstasis` from moving applications running as Administrator (e.g., elevated PowerShell) unless `winstasis` itself is executed from an elevated prompt. This is an accepted OS security feature, not a bug, and is logged gracefully as `[Failed]` during restore.

## 3. Next Steps
* Development on the core MVP is complete. 
* The project has successfully served its purpose as a pedagogical deep-dive into Win32 APIs and COM interfaces.
* Further exploration is shifting toward stable, lightweight offshoots (e.g., the `vdtree` Python inspector).
