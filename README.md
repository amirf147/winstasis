# 🪟 winstasis

> 🚀 **ACTIVE DEVELOPMENT: PHASE 4**  
> `winstasis` is entering its next major phase. While the core engine successfully manages high-precision window restoration on a single workspace, we are now integrating the [Slion/VirtualDesktop](https://github.com/Slion/VirtualDesktop) library to enable full multi-workspace session restoration.
> 
> **🌱 Completed Companion Tool:**
> - **[vdtree](https://github.com/amirf147/vdtree):** A lightweight, Python-based CLI tool that maps and lists all open windows across Virtual Desktops in a human-readable tree format.



---

**A lightweight, native C# Window Session Manager built for power users.**

*winstasis* is a command-line utility designed to capture, save, and seamlessly restore the exact positions, sizes, and states of Windows applications. It is built to tame the chaos of multi-workspace reboots and ensure that critical tools—including accessibility overlays and development environments—stay exactly where they belong.

## 🎯 Project Goals
This project serves a dual purpose:
1. **Utility:** Creating a "set-it-and-forget-it" tool to eliminate "window drift" and manual repositioning after system restarts.
2. **Pedagogy:** A deep-dive into C# and the Win32 API. This project is a stepping stone toward mastering Windows internals, with the long-term goal of developing advanced, low-level accessibility tools and high-performance window management solutions.

## 🗺️ Multi-Workspace Roadmap
While standard Windows APIs restrict moving windows across workspaces, we have identified a robust path forward for the next phase of development.

### 🔍 Future Implementation
The project will move from single-workspace restoration to full multi-workspace support by leveraging the [Slion/VirtualDesktop](https://github.com/Slion/VirtualDesktop) library. This candidate handles the complexity of Windows COM internals, allowing `winstasis` to:
1. **Switch Desktops Programmatically**: Navigate between workspaces to restore window contexts.
2. **Cross-Workspace Moves**: Move applications to their designated desktops regardless of owner permissions.


For more technical details on the research behind this, see:
- [Research: Virtual Desktop Libraries](docs/research_virtual_desktop_libraries.md)
- [Research: Windhawk, C++, and Memory Fragility](docs/research_windhawk_and_cpp.md)


## 🛠️ Core Accomplishments Before Pause
- **Hybrid Matching:** Reliable window resolution using HWND and Process/Title fallbacks.
- **Boundary Clamping:** Safe restoration across changing monitor topologies (e.g., docking/undocking).
- **JSON Orchestration:** Fully functional session capture and CLI routing.
- **Contextual Overrides:** Intelligent state management (e.g., waking minimized windows).

## 🧠 Development Methodology
This project was engineered using an **Agentic Workflow**. It leveraged highly iterative development cycles, AI-assisted pair programming within the Anti-Gravity editor, and a "Grill-with-Docs" strategy to ensure the codebase always matches its architectural decisions (ADRs).

## 🛠️ Usage
Ensure you have the .NET SDK installed.

### Commands
```bash
# Save current layout across ALL Virtual Desktops
dotnet run -- save coding

# Overwrite an existing profile
dotnet run -- save coding --force

# List all windows in a profile with Target IDs and Workspace GUIDs
dotnet run -- list coding

# Restore all windows in a profile (Active Desktop only, due to OS constraints)
dotnet run -- restore coding

# Restore a specific window by its Target ID
dotnet run -- restore coding --target 5
```