# 1. Hybrid Window Matching Strategy

**Status:** Accepted

## Context
`winst` needs to reliably identify windows to restore their exact coordinates. 
* Window Handles (`HWND`) are perfect, but they are volatile and destroyed upon app closure or system reboot.
* Window Titles are persistent across reboots, but can be brittle (e.g., changing active tabs) and ambiguous (multiple windows with the same title).
* A common user scenario involves accidentally closing a Virtual Desktop/Workspace, which dumps windows onto the main desktop but does *not* destroy the application or its `HWND`.

## Decision
We will implement a **Hybrid Matching Algorithm**:
1. **The Fast Path:** Attempt to match the saved `HWND`. If it exists, apply coordinates immediately (solves the closed-workspace scenario).
2. **The Fallback Path:** If the `HWND` is invalid (indicating a reboot or app restart), fall back to searching all open windows for a matching `Process Name` + `Window Title`.

## Consequences
* **Positive:** Significantly higher reliability for intra-session layout restoration.
* **Negative:** Requires saving volatile data (`HWND`) into persistent JSON storage.
* **Limitation:** Ambiguous matches (two windows with the exact same Process and Title post-reboot) will be resolved using a blind "First-Come, First-Served" approach.
