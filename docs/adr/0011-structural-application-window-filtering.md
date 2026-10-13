# 11. Structural Application Window Filtering

**Status:** Accepted

**Amends:** [ADR-0009](0009-two-pass-window-matching.md), [ADR-0010](0010-minimized-window-geometry-and-tool-window-filtering.md)

## Context
In [Issue #3](https://github.com/amirf147/winstasis/issues/3), users reported secondary floating windows persisting on screen after restoration. Investigation revealed that voice dictation utilities (such as ClickUp Brain MAX Talk to Text) maintain detached floating volume meters and push-to-talk pill overlays (`FLUTTER_RUNNER_WIN32_WINDOW`).

The v1.0.2 patch implemented a preliminary filter against `WS_EX_TOOLWINDOW` and literal `"Floating Window"` title strings in Pass 2. However, this approach presented three structural deficiencies:
1. **Pass 1 Handle Blind-Claim:** Pass 1 matched any active window handle whose process name matched the saved record, bypassing `IsAppWindow` validation. If a profile contained a legacy auxiliary window handle or if a handle was reused by a secondary pane, Pass 1 bound to it and executed window placement.
2. **Title String Heuristics:** Hardcoding checks against specific title strings made unwarranted assumptions about application naming conventions and failed when auxiliary windows used alternate titles, localized captions, or when saved profiles contained matching titles.
3. **Capture Divergence:** Window capture during `save` maintained duplicated, disparate Win32 P/Invoke filtering logic inside `Program.cs` instead of delegating to the unified `IWindowingEnvironment` abstraction.

## Decision
We replace title-based heuristics with deterministic Win32 structural classification across capture and restoration pipelines:

1. **Deterministic `IsAppWindow` Structural Classification:**
   In `WindowsEnvironmentAdapter.cs`, a window handle qualifies as an authentic top-level application window if and only if it satisfies all of the following Win32 structural conditions:
   * **Visibility:** Passes `IsWindow` and `IsWindowVisible`.
   * **Title Requirement:** Window title is non-empty after trimming.
   * **Extended Styles:** If `WS_EX_APPWINDOW` (`0x00040000`) is absent, the window must not possess `WS_EX_TOOLWINDOW` (`0x00000080`) or `WS_EX_NOACTIVATE` (`0x08000000`). Push-to-talk overlays, volume indicators, and on-screen keyboards utilize `WS_EX_NOACTIVATE` to avoid stealing focus during dictation.
   * **Hierarchy & Ownership:** The window must have no owner (`GW_OWNER == IntPtr.Zero`) and its root owner must resolve to itself (`GA_ROOTOWNER == hWnd`).
   * **Window Styles:** The window must not be a child (`WS_CHILD`). If styled as `WS_POPUP`, it must possess standard application window frame attributes (`WS_CAPTION`, `WS_THICKFRAME`, `WS_MINIMIZEBOX`, or `WS_MAXIMIZEBOX`). Borderless, unframed popups are rejected.
   * **DWM Cloaking:** The window must not be cloaked by its parent application (`DWMWA_CLOAKED` with `DWM_CLOAKED_APP` or `DWM_CLOAKED_INHERITED`). Note that `DWM_CLOAKED_SHELL` is retained to preserve multi-desktop tracking.
   * **Geometric Validity:** Visible windows must possess non-zero width and height (`width > 0 && height > 0`).

2. **Unified Capture Architecture:**
   In `Program.cs`, `HandleSaveCommand` discards duplicated native P/Invoke routines and delegates window enumeration directly to `IWindowingEnvironment.GetVisibleWindows()`. Both capture and restore pathways share identical qualification criteria.

3. **Restoration Guardrails in `WindowRestorer.cs`:**
   * **Pass 1 App Window Validation:** Fast-path HWND matching requires `_env.IsAppWindow(savedHwnd)`. If the target handle is an auxiliary overlay or tool window, Pass 1 rejects the assignment.
   * **Removal of Title-Specific Filtering:** Pass 2 strips hardcoded literal title checks, relying exclusively on structural `_env.IsAppWindow(hWnd)` validation. Tool windows and unactivated popups are never present in the candidate pool.

## Consequences
* **Positive:** Push-to-talk overlays, volume meters, docking hosts, and floating utility panes are excluded from snapshot capture and restore matching.
* **Positive:** Elimination of literal title matching ensures resilience across localized software, title mutations, and varied UI frameworks.
* **Positive:** Architectural alignment with ADR-0008 by routing all window qualification through `IWindowingEnvironment`.
