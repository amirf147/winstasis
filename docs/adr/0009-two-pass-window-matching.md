# 9. Two-Pass Window Matching with Exclusive Handle Allocation

**Status:** Accepted

**Supersedes:** [ADR-0001](0001-hybrid-window-matching.md)

## Context
ADR-0001 established a two-step hybrid matching strategy: check volatile `HWND` first, then fall back to exact `Process Name` + `Window Title`.

As identified by [@MrMxyzptlk](https://github.com/MrMxyzptlk) in [Issue #2](https://github.com/amirf147/winstasis/issues/2), exact title matching fails in real-world reboot workflows where applications dynamically alter their title bars on startup:
* Web browsers (`chrome.exe`) update titles based on the active tab or startup homepage.
* Note-taking tools (`Obsidian.exe`) update titles based on the active note and software version.
* Terminals and shells (`cmd.exe`) alter titles when snapshot commands terminate.
* File managers (`explorer.exe`, `XYplorer.exe`) alter titles based on directory navigation or upgrade notices.

Furthermore, stateless matching in ADR-0001 could return the identical `HWND` to multiple profile records if they shared process names and titles, leaving other running windows unmanaged.

## Decision
We implement a **Two-Pass Matching Pipeline** with **Exclusive Handle Allocation**:

1. **Pass 1 (Fast-Path and Strict Match):**
   * If the saved `HWND` is alive, visible, and belongs to the saved `ProcessName`, claim it immediately.
   * Otherwise, search for an unallocated window with an exact match on both `ProcessName` and `WindowTitle`.
2. **Pass 2 (Process Fallback):**
   * For any records unallocated after Pass 1, match remaining open windows belonging to the same `ProcessName`.
   * **Taskbar Safety Guard:** Explicitly filter out windows with empty or whitespace titles (`string.IsNullOrWhiteSpace`). This prevents matching Windows Taskbar surfaces (`Shell_TrayWnd`), desktop backgrounds (`Progman` / `WorkerW`), or invisible background utility windows.
3. **Exclusive Allocation (`claimedHwnds`):**
   * Maintain a `HashSet<long>` of claimed window handles during execution. Any handle claimed in Pass 1 or Pass 2 is excluded from subsequent searches, guaranteeing a 1-to-1 bijection between profile records and OS windows.

## Consequences
* **Positive:** Applications that mutate their title bars on startup are restored reliably after reboots without failing.
* **Positive:** Prevents multiple profile records from claiming or moving the identical physical window.
* **Positive:** Protects Windows system surfaces (`Shell_TrayWnd`) from accidental placement.
* **Limitation:** When multiple windows of the same process all experience title drift, Pass 2 matches them based on OS enumeration order (`EnumWindows` Z-order).
