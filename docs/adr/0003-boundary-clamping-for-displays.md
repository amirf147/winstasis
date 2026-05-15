# 3. Boundary Clamping for Multi-Monitor Topology

**Status:** Accepted

## Context
A user saves a window layout while connected to an external monitor (e.g., saving a window at X: 3000). Later, they disconnect the monitor and restore the layout on a smaller laptop screen (e.g., maximum X: 1920). If `winstasis` blindly applies the saved coordinates, the window will be thrown completely off-screen and become unreachable by the user.

An earlier draft considered a "nuclear abort" (refusing to restore if the monitor count/resolution changed), but this is too hostile to modern laptop users who frequently dock and undock.

## Decision
We will use **Boundary Clamping**. During a restore operation, `winstasis` will check the target coordinates against the bounds of the currently active display(s) using the Windows API (`MonitorFromRect` / `GetMonitorInfo`). If the target rectangle is completely outside the visible desktop area, `winstasis` will clamp its X and Y coordinates to the nearest visible edge before applying the move.

## Consequences
* **Positive:** Users can safely run restores regardless of whether they have plugged or unplugged monitors since the snapshot was taken.
* **Positive:** Prevents "lost" off-screen windows.
* **Negative:** The relative layout of windows might be squished or overlapping when shifting from a large dual-monitor setup down to a single screen, but this is an acceptable fallback compared to losing access to the application.
* **Note on Complexity:** While Boundary Clamping solves the "off-screen" problem, it does not account for complex coordinate translation across heterogeneous monitor layouts. Future phases (Phase 5) will explore more sophisticated repositioning logic.
