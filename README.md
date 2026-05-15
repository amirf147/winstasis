# 🪟 winstasis

**A lightweight, native C# Window Session Manager built for power users.**

*winstasis* is a command-line utility designed to capture, save, and seamlessly restore the exact positions, sizes, states, and **Virtual Desktops** of Windows applications. It is built to tame the chaos of multi-workspace reboots and ensure that critical tools—including accessibility overlays and development environments—stay exactly where they belong.

## 🎯 Project Goals
This project serves a dual purpose:
1. **Utility:** Creating a "set-it-and-forget-it" tool to eliminate "window drift" and manual repositioning after system restarts.
2. **Pedagogy:** A deep-dive into C# and the Win32 API. This project is a stepping stone toward mastering Windows internals, with the long-term goal of developing advanced, low-level accessibility tools and high-performance window management solutions.

## 🚀 Current Status: **Phase 4 (Omniscient Mover Complete)**
*winstasis* has achieved full 1:1 restoration for window positioning, geometry, and cross-workspace orchestration. 

> [!NOTE]
> **Single-Monitor Focus:** The current implementation is optimized and tested for single-monitor/laptop setups. While cross-workspace movement is fully functional, behavior during significant resolution changes or complex multi-monitor topology shifts is currently being explored.

### 🛑 Overcoming the "Access Denied" Barrier
During the development of Phase 3, we discovered that the official `IVirtualDesktopManager::MoveWindowToDesktop` API returns an **Access Denied (0x80070005)** error if a process attempts to move a window it does not explicitly own. This is a security design choice by Microsoft.

### 🔓 The Solution: `Slions.VirtualDesktop`
To move windows globally, we must use `IVirtualDesktopManagerInternal`. Because this interface is undocumented and changes its memory layout (`vtable`) with almost every major Windows update, we integrated the [Slion/VirtualDesktop](https://github.com/Slion/VirtualDesktop) NuGet package. This library manages the volatility by dynamically mapping memory offsets based on your exact Windows 11 build number, giving `winstasis` the "Holy Grail" of cross-workspace window movement.

## 🛠️ Core Accomplishments
- **Hybrid Matching:** Reliable window resolution using HWND and Process/Title fallbacks.
- **Boundary Clamping:** Safe restoration across changing monitor topologies (e.g., docking/undocking).
- **JSON Orchestration:** Fully functional session capture and CLI routing.
- **Contextual Overrides:** Intelligent state management (e.g., waking minimized windows).
- **Omniscient Workspace Orchestration:** Perfect restoration of windows to their correct Virtual Desktops.

## 🧠 Development Methodology
This project was engineered using an **Agentic Workflow**. It leveraged highly iterative development cycles, AI-assisted pair programming within the Anti-Gravity editor, and a "Grill-with-Docs" strategy to ensure the codebase always matches its architectural decisions (ADRs).

## 🛠️ Usage
Ensure you have the .NET SDK installed.

### Commands
```powershell
# Save current layout across ALL Virtual Desktops
dotnet run save coding

# Overwrite an existing profile
dotnet run save coding --force

# List all windows in a profile with Target IDs and Workspace GUIDs
dotnet run list coding

# Restore all windows in a profile (Across all Desktops!)
dotnet run restore coding

# Restore a specific window by its Target ID
dotnet run restore coding --target 5
```

> **Note on Elevated Windows:** Windows User Interface Privilege Isolation (UIPI) prevents normal applications from moving windows owned by an Administrator. To restore Admin-level apps (like an elevated PowerShell prompt), you must run `winstasis` from an Administrator terminal.

## 🛤️ Future Directions & Offshoots
The core MVP is complete. We are exploring stable offshoots that utilize the robust "Observer" engine we've built here:

*   **[vdtree: Verbose Window Inspector](https://github.com/amirf147/vdtree)**: A read-only CLI tool that provides deep, structured metadata about the Windows desktop layer in a human-readable tree format.
*   **Multi-Monitor Robustness**: Future testing and refinement for complex monitor topologies and resolution-dependent scaling.
*   **[Caster UIA Context Engine](docs/research_ui_automation_vs_win32.md)**: A micro-service utilizing Microsoft UI Automation (UIA) to track keyboard focus inside specific application panes to trigger ultra fine-grained, context-aware speech grammars for the Caster accessibility toolkit.
