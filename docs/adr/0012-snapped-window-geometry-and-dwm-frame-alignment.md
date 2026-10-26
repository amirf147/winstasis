# 12. Snapped Window Geometry and DWM Frame Alignment

**Status:** Accepted

**Amends:** [ADR-0008](0008-ports-and-adapters-for-os-seam.md), [ADR-0010](0010-minimized-window-geometry-and-tool-window-filtering.md), [ADR-0011](0011-structural-application-window-filtering.md)

## Context
Two critical geometric discrepancies emerged in snapped window orchestration across multi-monitor and ultrawide setups:

1. **Issue #8 (Snapped Window Shrinking on Ultrawide Displays):**
   When a window is snapped into a column or zone via Windows Snap Assist (`ShowCmd == 1`), the Windows kernel does not update `WINDOWPLACEMENT.rcNormalPosition`. Instead, `rcNormalPosition` retains stale, pre-snap floating dimensions (such as `1200x800`). In previous versions, snapshot capture relied on `rcNormalPosition` for all normal windows to prevent minimized coordinate corruption. Restoring an ultrawide snapped window caused it to revert to its stale floating dimensions instead of restoring its active snapped column bounds.

2. **Issue #7 (Visible Border Overlap and Clamping Creep):**
   Windows 10 and 11 add invisible resizing borders (`SM_CXSIZEFRAME + SM_CXPADDEDBORDER`, typically 7 pixels on left, right, and bottom) around top-level windows. Raw Win32 APIs (`GetWindowRect`, `SetWindowPlacement`) operate in raw frame coordinates including these invisible margins, whereas the Desktop Window Manager (`DwmGetWindowAttribute` with `DWMWA_EXTENDED_FRAME_BOUNDS`) reports actual visible pixel boundaries. In addition, when the calling process is DPI-unaware, `GetWindowRect` is DPI-virtualized while DWM queries return physical pixels. When two adjacent snapped windows were captured and restored using Win32 frame bounds, their invisible borders caused a 7 to 8 pixel visible overlap along adjacent edges, and boundary clamping in Win32 frame space pushed windows inward on monitor borders.

## Decision
We implement a four-part geometric normalization pipeline across capture, restoration, and environment boundaries:

1. **Per-Monitor V2 DPI Awareness (`DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2`):**
   `Program.Main` initializes `SetProcessDpiAwarenessContext((IntPtr)(-4))` at process entry. This ensures that all Win32 coordinates (`GetWindowRect`, `SetWindowPos`, `GetMonitorInfo`) and DWM coordinates (`DwmGetWindowAttribute`) operate in identical physical screen pixel spaces across displays with disparate DPI scaling.

2. **Unified `WindowCapturer` and Contextual 3-Way Capture:**
   Window capture logic is extracted from `Program.cs` into `WindowCapturer`, isolated behind `IWindowingEnvironment`. Geometry capture evaluates `WINDOWPLACEMENT.showCmd`:
   * **`ShowCmd == 1` (Normal / Snapped):** Captures visible pixel bounds via `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`. If DWM queries fail or return non-positive dimensions, it falls back to `GetWindowRect`.
   * **`ShowCmd == 2` (Minimized):** Captures `rcNormalPosition` to preserve restored dimensions and avoid off-screen placeholder coordinates (`-32000, -32000`).
   * **`ShowCmd == 3` (Maximized):** Captures `rcNormalPosition`. If the work area containing `rcNormalPosition` differs from the monitor on which the window is currently maximized, the normal position is translated into the active monitor's work area. This prevents un-maximizing onto the wrong display.

3. **Visible-Space Clamping and 3-Step Normal Restoration:**
   Restoration handles boundary clamping and frame compensation in visible coordinate space:
   * **Visible-Space Clamping (`ClampToNearestMonitor`):** Saved visible rectangles are clamped against `MONITORINFO.rcWork`. If an ultrawide window exceeds display bounds, width and height are clamped (`Math.Min(targetRect.Width, workArea.Width)`), and coordinates are shifted to remain within the active monitor.
   * **Step 1 (Placement Transition):** Calls `SetWindowPlacement` with `SW_SHOWNORMAL` (`1`). This moves the window to the target monitor, clears any Minimized (`2`), Maximized (`3`), or Windows 11 Snap Group arranged state, and triggers target-monitor `WM_DPICHANGED`.
   * **Step 2 (Live Margin Measurement):** Queries `GetFrameMargins(hWnd)` by comparing live `GetWindowBounds` against `GetVisibleWindowBounds`. Because margins can vary by monitor DPI and style flags, margins are measured after Step 1 has positioned the window.
   * **Step 3 (Compensated Positioning):** Inflates the clamped visible rectangle by the measured frame margins (`Left - margins.Left`, `Top - margins.Top`, `Right + margins.Right`, `Bottom + margins.Bottom`) and applies the result via `SetWindowPos` (`SWP_NOZORDER | SWP_NOACTIVATE`). This ensures that the window's visible edge aligns exactly with the target rectangle while the invisible resize borders extend beyond it.

4. **Structured Diagnostic Telemetry (`--verbose` / `-v`):**
   Order-independent CLI flag parsing in `Program.cs` enables verbose telemetry for `save` and `restore`. During capture and restore, `WindowDiagnostics` records HWND, window class name, Win32 style/exStyle bits, raw Win32 rect, visible DWM rect, normal position rect, frame margins, clamping deltas, and matching pass reasons (`Pass 1 Fast-Path HWND`, `Pass 1 Exact Title`, `Pass 2 Process Fallback`).

## Consequences
* **Positive:** Snapped windows on ultrawide monitors capture their active visible geometry instead of reverting to outdated pre-snap floating bounds.
* **Positive:** Adjacent snapped windows align flush with zero pixel gap or overlap.
* **Positive:** Moving windows between displays with disparate DPI or resolutions maintains correct proportions and positions.
* **Positive:** Architectural separation of concerns is maintained by keeping all native Win32/DWM interop inside `WindowsEnvironmentAdapter` and decoupling domain logic inside `WindowCapturer` and `WindowRestorer`.
