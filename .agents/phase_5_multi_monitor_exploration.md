# Phase 5: Multi-Monitor & Workspace Orchestration Refinement (Planned)
**Goal:** Expand the robust "Omniscient Mover" engine to handle complex monitor topologies and advanced Virtual Desktop features like pinned windows and workspace metadata.

## 1. Objectives
* **Topology Awareness:** Capture the current monitor configuration (monitor count, resolutions, and relative layout) during the `save` command.
* **Intelligent Coordinate Translation:** Beyond simple Boundary Clamping, implement logic to scale or reposition windows relatively if the target display resolution has changed.
* **Pinned Window Support:** Detect windows that are "pinned" to all Virtual Desktops. Ensure that upon restoration, these windows are re-pinned globally rather than restricted to a single workspace.
* **Workspace Metadata:** Capture and display human-readable workspace names (if available) or sequential numbers (e.g., "Workspace 1: Coding") to improve CLI clarity.
* **Scaling Compensation:** Handle Windows High-DPI scaling factors (e.g., 125% vs 100%) to ensure window sizes remain consistent across different displays.
* **Edge Case Testing:** Identify behavior when restoring a layout from a triple-monitor setup onto a single laptop screen, and vice versa.

## 2. Research Areas
* **`EnumDisplayMonitors` & `GetMonitorInfo`:** Deeper integration with Win32 monitor discovery.
* **`IVirtualDesktopPinnedApps`:** Investigating the COM interfaces required to detect and set the "pinned" status of windows.
* **Workspace Naming APIs:** Researching how to extract user-defined Virtual Desktop names from the Windows registry or internal COM interfaces.
* **Coordinate Mapping:** Translating relative positions across different monitor origins.

## 3. Success Criteria
* Windows are restored to the "best fit" display when the original monitor is missing.
* Pinned windows correctly appear on all desktops after a full profile restore.
* CLI output for `list` and `restore` commands includes descriptive workspace identifiers.
