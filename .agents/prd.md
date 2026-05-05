# Product Requirements Document: Window Session Orchestrator
**Document Status:** Draft - Iteration 1

## 1. Executive Summary
A lightweight, performant, native C# command-line utility designed to capture and restore the exact positions, sizes, and states of visible Windows applications.

## 2. Core Objectives
* **Performance:** Execute instantly with near-zero background resource consumption.
* **Simplicity:** Utilize a portable JSON format for session storage (no databases).
* **Targeted Control:** Capable of restoring entire multi-workspace environments or single, specific application windows (e.g., an accessibility HUD).

## 3. Scope & Requirements (MVP)
* **Trigger:** Command-line execution.
* **Data Capture:**
    * Process Name (e.g., `waterfox.exe`, `python.exe`)
    * Window Title
    * Coordinates (X, Y) and Dimensions (Width, Height)
    * Window State (Normal, Maximized, Minimized)
* **Storage:** Local JSON file (`session_snapshot.json`).
* **Filtering:** Must strictly ignore invisible system background processes and ghost windows.

## 4. High-Level Architecture Plan
This project will be built in isolated, testable modules:
* **Phase 1: The Observer (Spike Solution):** A script to read and print all currently visible windows and their coordinates.
* **Phase 2: Data Serialization:** Defining the C# classes and writing the exact layout to a JSON file.
* **Phase 3: The Mover:** Reading the JSON file and repositioning already-open windows to their saved coordinates.
* **Phase 4: Refinement:** Adding Virtual Desktop awareness and hardware-agnostic positioning (percentage-based coordinates).