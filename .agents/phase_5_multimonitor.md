# Phase 5: Multi-Monitor Topology Mapping (Deferred)

**Status:** Deferred to a future release.

## 1. Current State (Boundary Clamping)
Currently, `winstasis` handles multi-monitor scenarios using **Boundary Clamping** (ADR-0003). 
If a user saves a window on an external monitor at `X: 3000`, and then unplugs the monitor (leaving only a laptop screen with max `X: 1920`), `winstasis` will safely detect the out-of-bounds error and forcefully slide the window back onto the visible laptop screen when restoring.

## 2. The Deferred Goal
Boundary Clamping prevents "lost" off-screen windows, but it does *not* perfectly recreate complex multi-monitor topologies. 
If a user has 3 monitors (Left, Center, Right) and moves between the office and home with different monitor brands and resolutions, simply saving X/Y coordinates is insufficient.

**To perfectly restore layouts across multiple distinct hardware setups, `winstasis` would need to:**
1. Extract the hardware Serial Number / Device ID of every active physical monitor during a `save`.
2. Map the X/Y coordinates *relative* to that specific monitor, rather than relative to the global Windows virtual screen.
3. During a `restore`, cross-reference the currently attached monitors with the saved hardware IDs.
4. If a monitor is missing, apply a fallback "Monitor Mapping" strategy.

## 3. Reason for Deferral
This logic is highly complex and introduces significant edge cases regarding DPI scaling, resolution mismatches, and hardware hot-plugging. For the MVP, single-monitor or static multi-monitor setups are fully supported via Boundary Clamping. Deep hardware mapping will be explored in a later iteration.
