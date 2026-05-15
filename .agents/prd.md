# Product Requirements Document: Window Session Orchestrator
**Document Status:** Draft - Iteration 3 (Phase 4 Complete)

## 1. Executive Summary
A lightweight, performant, native C# command-line utility designed to capture and restore the exact positions, sizes, and states of visible Windows applications. It acts as a "state manager" for your workspace, allowing both bulk and single-window targeting across multiple Virtual Desktops.

## 2. Core Objectives
* **Performance:** Execute instantly with minimal background resource consumption.
* **Simplicity:** Utilize a portable JSON format for session storage (no databases).
* **Targeted Control:** Capable of restoring entire multi-workspace environments or single, specific application windows via generated Target IDs.

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