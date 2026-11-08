# 13. Multi-Signal Cohort Bipartite Window Matching

**Status:** Accepted

**Supersedes:** [ADR-0009](0009-two-pass-window-matching.md)

**Preserves & Integrates:** [ADR-0008](0008-ports-and-adapters-for-os-seam.md), [ADR-0010](0010-minimized-window-geometry-and-tool-window-filtering.md), [ADR-0011](0011-structural-application-window-filtering.md), [ADR-0012](0012-snapped-window-geometry-and-dwm-frame-alignment.md)

## Context
In [Issue #10](https://github.com/amirf147/winstasis/issues/10), multi-instance applications (`Antigravity IDE.exe`, `waterfox.exe`) experienced circular virtual desktop and geometry swaps during restoration after application restarts and system reboots.

Empirical trace analysis across 10 virtual desktops identified three root causes in the [ADR-0009](0009-two-pass-window-matching.md) matching pipeline:

1. **Single-Broker Process Indistinguishability:** Modern multi-window applications (Electron IDEs, Chromium and Gecko browsers, Windows Explorer) broker all top-level `HWND` containers through a single parent process ID (`PID`) with an identical command line. OS process identifiers cannot differentiate instances within a process cohort.
2. **Order-Dependent Greedy Theft in Pass 2:** When active file tabs or browser tabs change across multiple instances of a process, Pass 1 exact title matching fails for those instances. Pass 2 iterates through unallocated profile records sequentially by `TargetId` and greedily claims the first unallocated `HWND` in OS `EnumWindows` Z-order. A lower `TargetId` claims whichever window happens to sit highest in the Z-order, triggering a cascading permutation of geometries and virtual desktop assignments across the remaining records.
3. **Active-Tab Information Deficit in `WindowRecord`:** Recording only `GetWindowText` captures a single active tab out of potentially dozens of open tabs in a browser or terminal container. When multiple browser windows are maximized (`ShowCmd == 3`) on the same monitor with identical `rcNormalPosition` bounds (`0,0 1920x1168`) and their active tabs change, neither `GetWindowText` nor `WINDOWPLACEMENT` contains sufficient information to disambiguate the windows. Furthermore, raw `HWND` integers used in Pass 1 are 16-bit handle table indices that the OS kernel can recycle across reboots.

At the same time, any replacement matching architecture must preserve all prior stability guarantees established across [Issue #2](https://github.com/amirf147/winstasis/issues/2), [Issue #3](https://github.com/amirf147/winstasis/issues/3), [Issue #4](https://github.com/amirf147/winstasis/issues/4), [Issue #7](https://github.com/amirf147/winstasis/issues/7), and [Issue #8](https://github.com/amirf147/winstasis/issues/8). Specifically, single-instance applications whose titles mutate completely on startup must still restore ([Issue #2](https://github.com/amirf147/winstasis/issues/2)), auxiliary tool windows and `WS_EX_NOACTIVATE` overlays must never be claimed ([Issue #3](https://github.com/amirf147/winstasis/issues/3)), minimized geometry must remain protected ([Issue #4](https://github.com/amirf147/winstasis/issues/4)), and 3-way contextual geometry capture with DWM frame margin compensation must remain intact ([Issue #7](https://github.com/amirf147/winstasis/issues/7), [Issue #8](https://github.com/amirf147/winstasis/issues/8)).

## Decision
We replace the greedy two-pass matching loop with a **Two-Stage Multi-Signal Bipartite Matching Pipeline** isolated behind `IWindowingEnvironment` and a pure domain service (`WindowMatcher`):

### 1. Kernel Property Stamping (`SetPropW` / `GetPropW`)
* Extend `WindowRecord` with a 64-bit `InstanceToken` (`long`).
* During `save` and `restore`, `WindowsEnvironmentAdapter` stamps `InstanceToken` directly onto the OS window handle's kernel property list via Win32 `SetPropW(hWnd, "WinStasis.InstanceToken", (IntPtr)token)`.
* In **Stage 1 (Verified Intra-Session Fast-Path)**, if a live candidate `HWND` of the same `ProcessName` carries a kernel property matching `win.InstanceToken` via `GetPropW`, it is claimed immediately. Because kernel window properties are destroyed when an `HWND` closes, post-reboot handle recycling can never cause a false-positive match.
* For backward compatibility with profiles lacking `InstanceToken`, Stage 1 falls back to matching `savedHwnd` when the handle is alive, passes `IsAppWindow`, matches `ProcessName`, and its live title is not exclusively claimed by another record in the same cohort.

### 2. Opportunistic Shallow Tab Inspection & UIA Scope Limitation
* Revise the Opaque Window Rule in `CONTEXT.md`: while `winst` does not mutate internal application state or perform deep DOM traversal, it performs opportunistic, non-blocking container tab enumeration (`UIA_TabItemControlTypeId = 50019`) via native COM `IUIAutomation` (`UIAutomationCore.dll`).
* `WindowsEnvironmentAdapter.GetWindowTabs(long hwnd)` queries `FindAllBuildCache` with a property condition restricting results to `ControlType == TabItem` and bulk-fetches `UIA_NamePropertyId` in a single cross-process call. All COM exceptions, timeouts, or UIPI access denials are caught and return `Array.Empty<string>()`.
* To traverse offscreen or cloaked window subtrees across inactive Virtual Desktops (`DWM_CLOAKED_SHELL`), `UiAutomationTabReader` configures the UIA cache request with `rawView` (`uia.get_RawViewCondition`).
* For `MozillaWindowClass` (`waterfox.exe`), Gecko lazily initializes its accessibility bridge upon external query. If the initial query returns zero elements, `UiAutomationTabReader` dispatches `SendMessageTimeoutW(hWnd, WM_GETOBJECT, IntPtr.Zero, (IntPtr)OBJID_CLIENT, SMTO_ABORTIFHUNG, 100, out _)` to activate the accessible root, waits 50ms, and retries `FindAllBuildCache`.
* Static developer environment bottom panels (`Problems`, `Output`, `Debug Console`, `Terminal`, `Ports`, `Comments`) are excluded to preserve pure document and browser navigation tabs.
* `WindowCapturer` records `string[] Tabs` on each `WindowRecord` during `save`. During `restore`, live tab sets are queried lazily only for unallocated multi-instance cohorts (`M > 1` or `K > 1`).
* **Empirical Scope & Deferred Deep Inspection:** Empirical trace validation demonstrated that modern browser engines (Gecko and Chromium) do not reliably expose top-level browser chrome tabs as `UIA_TabItemControlTypeId` without non-standard command-line flags, instead exposing in-page ARIA tabs. More importantly, cohort resolution across multi-instance IDEs and browsers is fully achieved through Cohort Workspace Anchors (Tier 2) and Virtual Desktop Affinity (Tier 4). Tab-strip matching is therefore retained solely as an opportunistic auxiliary tier (Tier 3), while deep application-specific tab inspection is formally deferred to dedicated offshoots (such as the Caster UIA Context Engine). WinStasis core does not depend on UIA to guarantee deterministic restoration.

### 3. Cohort-Relative Structural Caption Decomposition
* Window captions are decomposed along structural delimiters (`" - "`, `" \u2014 "`, `" \u2013 "`, `" | "`, `" \u00b7 "`).
* Within each `ProcessName` cohort, segment document frequency is evaluated across the cohort. Segments appearing across all instances of a multi-window cohort (such as `"Antigravity IDE"` or `"Waterfox"`) are classified as process boilerplate and assigned zero discriminative weight.
* Segments that uniquely identify a single saved record and a single live candidate within the cohort (such as project workspace names `"WinStasis"`, `"YouTube-Channel"`, or vault names) receive high-weight **Workspace Anchor** scoring.

### 4. Per-Process Global Bipartite Assignment (Kuhn-Munkres Algorithm)
* In **Stage 2 (Cohort Bipartite Matching)**, unallocated profile records are grouped by `ProcessName`.
* Candidate live windows for each process are filtered strictly through `!string.IsNullOrWhiteSpace(title)` and `IsAppWindow(hWnd)` ([ADR-0011](0011-structural-application-window-filtering.md)).
* For a process cohort with $M$ unallocated saved records and $K$ unallocated live candidate windows, `WindowMatcher` constructs an $M \times K$ similarity matrix utilizing strict non-overlapping lexicographical priority tiers:
  1. **Tier 1: Exact Caption Match (`+100000.0`):** Exact ordinal string equality between saved and live `WindowTitle`. Telemetry label: `Pass 2 (Bipartite: Exact Title)`.
  2. **Tier 2: Unique Cohort Workspace Anchor (`+10000.0`):** Unique non-boilerplate segment anchor matching exactly one saved record and one live candidate in the cohort. Telemetry label: `Pass 2 (Bipartite: Workspace Anchor)`.
  3. **Tier 3: Background Tab Overlap or Caption Overlap (`0.0` to `+1000.0`):** Szymkiewicz-Simpson and Jaccard overlap on tab-strips (`0.0` to `+1000.0`) or non-unique caption Jaccard similarity (`0.0` to `+500.0`). Telemetry label: `Pass 2 (Bipartite: Tab Overlap)` or `Pass 2 (Bipartite: Caption Overlap)`.
  4. **Tier 4: Virtual Desktop Residence (`+200.0`):** Match on active `DesktopId` (`r.Record.DesktopId != Guid.Empty && r.Record.DesktopId == w.DesktopId`). Telemetry label: `Pass 2 (Bipartite: Desktop Affinity)`. This tier strictly dominates contextual geometry so that hidden `rcNormalPosition` coordinates cannot override virtual desktop affinity.
  5. **Tier 5: Contextual Geometry Similarity (`0.0` to `+50.0`):** `ShowCmd` equality (`+10.0`), normalized size similarity (`0.0` to `+25.0`), and normalized coordinate proximity (`0.0` to `+15.0`), comparing saved bounds against live 3-way contextual bounds ([ADR-0012](0012-snapped-window-geometry-and-dwm-frame-alignment.md)). Telemetry label: `Pass 2 (Bipartite: Geometry)`. If geometry does not distinguish candidates (`geomVariance < 1e-4` or single candidate), telemetry reports `Pass 2 (Bipartite: Order Fallback)`.
  6. **Tier 6: Relative Z-Order Alignment (`0.0` to `+5.0`):** Tie-breaker based on relative index alignment between saved profile index and live enumeration order. Telemetry label: `Pass 2 (Bipartite: Order Fallback)`.
* The $M \times K$ matrix is solved globally using the **Kuhn-Munkres (Hungarian) algorithm** in $O(\max(M, K)^3)$ time. Because all valid candidate pairs have non-negative scores ($\ge 0.0$) and dummy padding entries carry weight `-1.0`, the solver is guaranteed to assign $\min(M, K)$ windows, preserving single-instance title-drift restoration ([Issue #2](https://github.com/amirf147/winstasis/issues/2)) and isolating completely mutated instances by elimination.

## Consequences
* **Positive:** Eliminates multi-window virtual desktop and geometry swaps across IDEs, editors, browsers, and terminals after reboots or application restarts.
* **Positive:** Identifies maximized browser windows with identical dimensions even when active tabs change, using background tab-strip overlap.
* **Positive:** Global bipartite optimization guarantees that high-confidence matches lock in first and resolve ambiguous or completely mutated windows by elimination, regardless of `TargetId` or OS Z-order.
* **Positive:** Preserves all existing filters and geometric rules from ADR-0010, ADR-0011, and ADR-0012 without regression.

