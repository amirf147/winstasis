# Product Requirements Document: Window Session Orchestrator
**Document Status:** Draft - Iteration 2 (Post-Pivot)

## 1. Executive Summary
A lightweight, performant, native C# command-line utility designed to capture and restore the exact positions, sizes, and states of visible Windows applications. It acts as a "state manager" for your workspace, allowing both bulk and single-window targeting.

## 2. Core Objectives
* **Performance:** Execute instantly with near-zero background resource consumption.
* **Simplicity:** Utilize a portable JSON format for session storage (no databases).
* **Targeted Control:** Capable of restoring entire multi-workspace environments or single, specific application windows via generated Target IDs.

## 3. High-Level Architecture Plan
* **Phase 1: The Observer (Spike):** Extracting window data cleanly from the OS. *(Completed)*
* **Phase 2: The State Manager (CLI & Storage):** Implementing `save <profile>` and `list <profile>` so users can see stable Target IDs for each window in a snapshot.
* **Phase 3: The Mover (Restore):** Implementing full profile restores AND single-target restores (`winstasis restore coding --target 4`), including Boundary Clamping and Contextual State Overrides.
* **Phase 4: Virtual Desktop Awareness:** Adding official COM interface hooks to filter windows by the currently active Virtual Desktop.

## 4. Architectural Rules
* **Active Workspace Scoping:** Only manage windows on the currently active Virtual Desktop.
* **Opaque Window Rule:** We push boxes around the screen. We do not inspect tabs, internal app states, or attempt to launch closed `.exe` files.
* **Boundary Clamping:** Windows restored outside active monitor bounds are shifted inside the nearest visible edge.
* **Overwrite Protection:** Accidental profile overwrites are blocked unless a `--force` flag is supplied.