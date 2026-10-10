# 10. Minimized Window Geometry Preservation and Auxiliary Tool Window Filtering

**Status:** Accepted

**Amends:** [ADR-0009](0009-two-pass-window-matching.md)

## Context
Following the release of v1.0.1, [@MrMxyzptlk](https://github.com/MrMxyzptlk) identified two related regressions in [Issue #3](https://github.com/amirf147/winstasis/issues/3) and [Issue #4](https://github.com/amirf147/winstasis/issues/4):
1. **Minimized Window Geometry Corruption (Issue #4):** During `save`, `GetWindowRect` returns off-screen placeholder coordinates `(-32000, -32000)` with compact icon dimensions (136 to 170 pixels wide by 28 to 39 pixels high) for minimized applications (`ShowCmd == 2`). During `restore`, `ClampToNearestMonitor` shifts these coordinates onto visible monitor bounds, setting `NormalPosition` to the placeholder rectangle. When the user subsequently unminimizes the window from the taskbar, Windows restores the application as a tiny unclickable box.
2. **Auxiliary Tool Window False Matching (Issue #3):** Pass 2 in ADR-0009 matched any window sharing the target `ProcessName` as long as its title was non-empty. Multi-window applications and docking frameworks frequently spawn tool windows, floating palettes, and preview panes titled `"Floating Window"` or with secondary helper captions. Pass 2 bound saved application profiles to these auxiliary tool windows instead of main application interfaces.

## Decision
We implement a two-part architectural remediation:

1. **Geometry Preservation via `rcNormalPosition`:**
   * In `Program.cs`, geometry capture reads `WINDOWPLACEMENT.rcNormalPosition` instead of `GetWindowRect` whenever a window is minimized (`ShowCmd == 2`) or maximized (`ShowCmd == 3`). `rcNormalPosition` contains the verified restored dimensions.
   * In `WindowRestorer.cs`, restore operations inspect saved geometries for legacy corrupted placeholder coordinates (`X <= -10000`, `Y <= -10000`, or `ShowCmd == 2` with width below 200 pixels). When detected, `NormalPosition` is not overwritten, preserving the running application's valid restored dimensions.

2. **Top-Level Application Window Filtering:**
   * Extend `IWindowingEnvironment` and `WindowsEnvironmentAdapter` with `IsAppWindow`.
   * A window is excluded from enumeration and matching if it carries the `WS_EX_TOOLWINDOW` extended style without `WS_EX_APPWINDOW`, or if it possesses an owner handle (`GW_OWNER != IntPtr.Zero`) without `WS_EX_APPWINDOW`.
   * Pass 2 explicitly rejects windows with literal titles equal to `"Floating Window"` when the saved profile record had a distinct title.

## Consequences
* **Positive:** Minimized startup applications unminimize to their authentic dimensions without geometric distortion.
* **Positive:** Legacy profiles saved prior to this patch no longer corrupt restored dimensions when applied.
* **Positive:** Pass 2 no longer binds saved application records to floating palettes, docking hosts, or notification windows.
