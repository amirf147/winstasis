# Product Requirements Document: Window Session Orchestrator
**Document Status:** Draft - Iteration 4 (Refined Scope)

## 1. Executive Summary
A lightweight, performant, native C# command-line utility designed to capture and restore the exact positions, sizes, and states of visible Windows applications. It acts as a "state manager" for your workspace, allowing both bulk and single-window targeting across multiple Virtual Desktops.

## 2. Core Objectives
* **Performance:** Execute instantly with minimal background resource consumption.
* **Simplicity:** Utilize a portable JSON format for session storage (no databases).
* **Targeted Control:** Capable of restoring entire multi-workspace environments or single, specific application windows via generated Target IDs.
* **Pinned Window Support (Roadmap):** Capability to identify and re-apply "global" status to windows that are pinned to all Virtual Desktops.
* **Multi-Monitor Robustness (Roadmap):** Evolve coordinate handling to remain stable across heterogeneous monitor topologies and high-DPI scaling shifts.

## 3. High-Level Architecture Plan
* **Phase 1: The Observer (Spike):** Extracting window data cleanly from the OS. *(Completed)*
* **Phase 2: The State Manager (CLI & Storage):** Implementing `save <profile>` and `list <profile>` so users can see stable Target IDs for each window in a snapshot. *(Completed)*
* **Phase 3: The Mover (Restore):** Implementing full profile restores AND single-target restores (`winstasis restore coding --target 4`), including Boundary Clamping and Contextual State Overrides. *(Completed)*
* **Phase 4: Omniscient Workspace Orchestration:** Integrating `Slions.VirtualDesktop` to enable 1:1 cross-workspace window movement, bypassing the OS `Access Denied` limitations. *(Completed)*

## 4. Architectural Rules
* **Omniscient Workspace Tracking:** The tool extracts and manages windows across *all* active Virtual Desktops, not just the current one.
* **Opaque Window Rule:** We push boxes around the screen. We do not inspect tabs, internal app states, or attempt to launch closed `.exe` files.
* **Boundary Clamping:** Windows restored outside active monitor bounds are shifted inside the nearest visible edge.
* **Overwrite Protection:** Accidental profile overwrites are blocked unless a `--force` flag is supplied.

## 5. Current Constraints & Assumptions
* **Single-Monitor Focus:** The current implementation is designed for a single primary display. While multi-monitor setups *may* work if topology is identical between save and restore, they are not yet officially supported.
* **Resolution Stability:** Significant resolution changes (e.g., docking a laptop) rely on Boundary Clamping fallback, which may cause overlapping windows.
* **Workspace Naming:** Currently, workspaces are identified by their internal OS GUIDs. Human-readable names (or sequential numbers) are planned for Phase 5 to improve usability.